using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;

namespace HextechRunes;

/// <summary>
/// 原版拷贝守卫:凡本模组用 <c>bool</c> 前缀可能跳过原方法的目标,都可能复制了一段原版逻辑;
/// 另有少数补丁不跳过原方法、却在 Postfix 里复制原方法步骤,由 <see cref="HextechPatchAttribute.CopiesVanillaLogic"/> 声明纳入。
/// 游戏更新后这些拷贝会静默失真,因此启动时比对目标方法 IL 的 SHA1 与冻结表,漂移即告警。
/// 哈希前先把元数据 token 解析成成员描述(见 <see cref="NormalizeIl"/>),各平台分别编译的同版本程序集共用一张表。
/// </summary>
/// <remarks>
/// 目标清单来自 0.111.0 的 <c>HEXTECH_DUMP_PATCHES</c> 补丁表导出;各维护版本的行(含异步方法的
/// <c>MoveNext</c>)由测试 <c>VanillaCopyGuardFreezesEntriesAndAsyncBodies</c> 在对应 sts2.dll 上
/// 用 <c>HEXTECH_WRITE_PATCH_MANIFEST=1</c> 补齐,漂移行只报告不刷新。以嵌入资源
/// <c>vanilla_copy_guard.txt</c> 随各变体打包;没有表的变体跳过校验。
/// 行格式:<c>Namespace.Type::Method(ParamType,...)=sha1</c>,sha1 取自 token 归一化后的 IL。
/// </remarks>
internal static class HextechVanillaCopyGuard
{
	private const string ResourceName = "vanilla_copy_guard.txt";

	private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
	private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

	static HextechVanillaCopyGuard()
	{
		foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
		{
			if (field.GetValue(null) is OpCode opCode)
			{
				ushort value = (ushort)opCode.Value;
				(opCode.Size == 1 ? OneByteOpCodes : TwoByteOpCodes)[value & 0xFF] = opCode;
			}
		}
	}

	internal static string DescribeTarget(MethodBase method)
	{
		string parameters = string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name));
		return $"{method.DeclaringType?.FullName}::{method.Name}({parameters})";
	}

	internal static string? ComputeIlHash(MethodBase method)
	{
		byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
		return il == null ? null : Convert.ToHexString(SHA1.HashData(NormalizeIl(method, il))).ToLowerInvariant();
	}

	// 同一提交的 sts2.dll 在 Windows、macOS arm64、macOS x86_64 上各自编译，元数据 token 编号不同而 IL 指令相同
	// （0.111.0 三份程序集的守卫目标里原始 IL 有 117 个不同，token 清零后全部一致）。直接哈希原始字节会让
	// 非冻结平台整表误报 DRIFT；这里把 token 操作数换成所指成员或字符串的稳定描述再哈希，
	// 一张表跨平台通用，改调用目标、改字面量这类真实改动仍会漂移。
	private static byte[] NormalizeIl(MethodBase method, byte[] il)
	{
		using MemoryStream stream = new();
		using BinaryWriter writer = new(stream, Encoding.UTF8);
		Type[]? typeArguments = method.DeclaringType is { IsGenericType: true } declaringType ? declaringType.GetGenericArguments() : null;
		Type[]? methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
		int offset = 0;
		while (offset < il.Length)
		{
			OpCode opCode = il[offset] == 0xFE ? TwoByteOpCodes[il[offset + 1]] : OneByteOpCodes[il[offset]];
			offset += opCode.Size;
			writer.Write(opCode.Value);
			switch (opCode.OperandType)
			{
				case OperandType.InlineField:
				case OperandType.InlineMethod:
				case OperandType.InlineType:
				case OperandType.InlineTok:
				case OperandType.InlineString:
				case OperandType.InlineSig:
					writer.Write(DescribeToken(method.Module, opCode.OperandType, BitConverter.ToInt32(il, offset), typeArguments, methodArguments));
					offset += 4;
					break;
				case OperandType.InlineSwitch:
					int length = 4 + 4 * BitConverter.ToInt32(il, offset);
					writer.Write(il, offset, length);
					offset += length;
					break;
				default:
					int size = OperandSize(opCode.OperandType);
					writer.Write(il, offset, size);
					offset += size;
					break;
			}
		}

		writer.Flush();
		return stream.ToArray();
	}

	private static string DescribeToken(Module module, OperandType operandType, int token, Type[]? typeArguments, Type[]? methodArguments)
	{
		try
		{
			if (operandType == OperandType.InlineString)
			{
				return "str:" + module.ResolveString(token);
			}

			// calli 的独立签名是 blob，里面的类型同样按平台相关的 token 编码，难以稳定描述；只记操作数类别。
			if (operandType == OperandType.InlineSig)
			{
				return "sig";
			}

			MemberInfo? member = module.ResolveMember(token, typeArguments, methodArguments);
			return member is Type type ? "type:" + type : $"member:{member?.DeclaringType}::{member}";
		}
		catch (Exception ex) when (ex is ArgumentException or BadImageFormatException or TypeLoadException or MissingMemberException)
		{
			// 解析不了时只保留元数据表种类(token 高字节)，不能把平台相关的行号写进哈希。
			return "unresolved:" + (token >>> 24).ToString("x2");
		}
	}

	private static int OperandSize(OperandType operandType)
	{
		return operandType switch
		{
			OperandType.InlineNone => 0,
			OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
			OperandType.InlineVar => 2,
			OperandType.InlineI8 or OperandType.InlineR => 8,
			_ => 4,
		};
	}

	// 异步方法本体只是启动状态机的桩，原版逻辑改动几乎都落在编译器生成的 MoveNext 里；
	// 只哈希入口会让"跳过原方法并复制其逻辑"的前缀在游戏更新后静默失真。
	internal static IEnumerable<MethodBase> WithAsyncBody(MethodBase method)
	{
		yield return method;
		if (TryGetAsyncMoveNext(method) is MethodInfo moveNext)
		{
			yield return moveNext;
		}
	}

	internal static MethodInfo? TryGetAsyncMoveNext(MethodBase method)
	{
		Type? stateMachine = method.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()?.StateMachineType;
		return stateMachine?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	}

	/// <summary>本模组挂了可跳过原方法的前缀、或声明复制原版逻辑的补丁的所有目标。</summary>
	internal static IEnumerable<MethodBase> EnumerateGuardedTargets(string ownerId)
	{
		foreach (MethodBase method in Harmony.GetAllPatchedMethods())
		{
			Patches? info = Harmony.GetPatchInfo(method);
			if (info == null)
			{
				continue;
			}

			if (info.Prefixes.Any(patch => patch.owner == ownerId && HextechPatcher.IsSkipCapable(patch))
				|| info.Postfixes.Any(patch => patch.owner == ownerId && DeclaresVanillaCopy(patch)))
			{
				yield return method;
			}
		}
	}

	private static bool DeclaresVanillaCopy(Patch patch)
	{
		return patch.PatchMethod.DeclaringType?.GetCustomAttribute<HextechPatchAttribute>()?.CopiesVanillaLogic == true;
	}

	internal static IReadOnlyDictionary<string, string> LoadExpectedHashes()
	{
		Dictionary<string, string> expected = new(StringComparer.Ordinal);
		using Stream? stream = typeof(HextechVanillaCopyGuard).Assembly.GetManifestResourceStream(ResourceName);
		if (stream == null)
		{
			return expected;
		}

		using StreamReader reader = new(stream, Encoding.UTF8);
		while (reader.ReadLine() is string line)
		{
			string trimmed = line.Trim();
			int separator = trimmed.LastIndexOf('=');
			if (trimmed.Length == 0 || trimmed.StartsWith('#') || separator <= 0)
			{
				continue;
			}

			expected[trimmed[..separator]] = trimmed[(separator + 1)..];
		}

		return expected;
	}

	internal static void Verify(string ownerId)
	{
		try
		{
			IReadOnlyDictionary<string, string> expected = LoadExpectedHashes();
			if (expected.Count == 0)
			{
				HextechLog.Info("VanillaCopyGuard", $"No frozen IL table for compat target {ModInfo.TargetGameVersion}; skipping.");
				return;
			}

			List<string> drifted = [];
			List<string> unregistered = [];
			int verified = 0;
			foreach (MethodBase method in EnumerateGuardedTargets(ownerId).SelectMany(WithAsyncBody))
			{
				string key = DescribeTarget(method);
				string? actual = ComputeIlHash(method);
				if (!expected.TryGetValue(key, out string? frozen))
				{
					unregistered.Add(key);
				}
				else if (!string.Equals(frozen, actual, StringComparison.Ordinal))
				{
					drifted.Add($"{key} frozen={frozen} actual={actual ?? "<no body>"}");
				}
				else
				{
					verified++;
				}
			}

			if (drifted.Count > 0)
			{
				HextechLog.Warn("VanillaCopyGuard", $"DRIFT: {drifted.Count} patched method(s) changed IL since the table was frozen; review the prefixes that replace vanilla logic:\n  {string.Join("\n  ", drifted)}");
			}

			if (unregistered.Count > 0)
			{
				HextechLog.Info("VanillaCopyGuard", $"{unregistered.Count} skip-capable target(s) not in the frozen table:\n  {string.Join("\n  ", unregistered)}");
			}

			if (drifted.Count == 0)
			{
				// 冻结表覆盖所有版本的所有补丁目标，本变体实际打上并核对通过的才是 verified。
				HextechLog.Info("VanillaCopyGuard", $"{verified} patched target(s) verified against {expected.Count} frozen row(s).");
			}
		}
		catch (Exception ex)
		{
			HextechLog.Warn("VanillaCopyGuard", $"Verification failed: {ex.GetType().Name}: {ex.Message}");
		}
	}
}
