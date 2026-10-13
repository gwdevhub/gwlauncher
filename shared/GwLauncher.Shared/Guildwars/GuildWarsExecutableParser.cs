using PeNet;
using PeNet.Header.Pe;

namespace GW_Launcher.Guildwars;

internal sealed class GuildWarsExecutableParser
{

	private readonly PeFile peFile;
	private readonly byte[] buffer;
	private readonly ImageSectionHeader textSection;
	private readonly ImageSectionHeader rdataSection;

	public GuildWarsExecutableParser(string path)
	{
		this.peFile = new PeFile(path);
		this.buffer = this.peFile.RawFile.ToArray();
		this.textSection = this.peFile.ImageSectionHeaders?.FirstOrDefault(s => s.Name == ".text") ?? throw new InvalidOperationException("Unable to find .text section");
		this.rdataSection = this.peFile.ImageSectionHeaders?.FirstOrDefault(s => s.Name == ".rdata") ?? throw new InvalidOperationException("Unable to find .rdata section");
	}

	public int GetFileId()
	{
		var eulaAssertion = "index < arrsize(s_eula)"u8.ToArray();
		var eulaAssertionAddr = this.Find(eulaAssertion, new string('x', eulaAssertion.Length), 0, this.rdataSection);
		if (eulaAssertionAddr == 0)
			return 0;
		var imageBase = this.peFile.ImageNtHeaders!.OptionalHeader.ImageBase;
		var b = BitConverter.GetBytes((uint)(imageBase + eulaAssertionAddr));
		byte[] useOfAssertionPattern = [0xb9, b[0], b[1], b[2], b[3], 0xe8];
		var useOfAssertion = this.Find(useOfAssertionPattern, "xxxxxx");
		if (useOfAssertion == 0)
			return 0;

		var fileIdFunctionRva = this.FindInRange([0xb8, 0x00, 0x00, 0x00, 0x00, 0xc3], "x????x", 0, useOfAssertion, useOfAssertion - 0xff);
		if (fileIdFunctionRva == 0)
			return 0;

		return (int)this.Read(fileIdFunctionRva + 1);
	}

	/// <summary>Scans for a pattern across an entire section (defaults to .text).</summary>
	public uint Find(byte[] pattern, string mask, int offset = 0, ImageSectionHeader? section = null)
	{
		section ??= this.textSection;
		var start = section.VirtualAddress;
		var end = start + section.VirtualSize;
		return this.FindInRange(pattern, mask, offset, start, end);
	}

	/// <summary>
	/// Searches the .text section for a PUSH imm32 / CALL pair referencing the given RVA.
	/// </summary>
	public uint FindPushCall(uint rdataRva)
	{
		var imageBase = this.peFile.ImageNtHeaders!.OptionalHeader.ImageBase;
		var b = BitConverter.GetBytes((uint)(imageBase + rdataRva));
		// PUSH imm32 (0x68) <4-byte LE VA> CALL (0xE8)
		return this.Find([0x68, b[0], b[1], b[2], b[3], 0xE8], "xxxxxx");
	}

	/// <summary>
	/// Scans for a pattern between two RVAs using a classic 'x'/'?' mask string.
	/// If <paramref name="end"/> is less than <paramref name="start"/>, scans backwards.
	/// Returns the RVA of the match plus <paramref name="offset"/>, or 0 if not found.
	/// </summary>
	/// <param name="pattern">Raw byte pattern to search for.</param>
	/// <param name="mask">Same length as <paramref name="pattern"/>. 'x' = exact match, '?' = wildcard.</param>
	/// <param name="offset">Added to the matched RVA before returning.</param>
	/// <param name="start">RVA to begin scanning from (inclusive).</param>
	/// <param name="end">RVA to scan up to (exclusive). If less than <paramref name="start"/>, scans backwards.</param>
	public uint FindInRange(byte[] pattern, string mask, int offset, uint start, uint end)
	{
		if (pattern.Length != mask.Length)
			throw new ArgumentException("Pattern and mask must be the same length.");

		bool Matches(int filePos)
		{
			for (var j = 0; j < pattern.Length; j++)
			{
				if (mask[j] == 'x' && this.buffer[filePos + j] != pattern[j])
					return false;
			}
			return true;
		}

		if (end >= start)
		{
			// Forward scan: [start, end)
			var from = this.RvaToOffset(start);
			var to = this.RvaToOffset(end) - pattern.Length;
			for (var i = from; i <= to; i++)
			{
				if (Matches(i)) return start + (uint)(i - from) + (uint)offset;
			}
		}
		else
		{
			// Backward scan: (end, start]
			var from = this.RvaToOffset(start) - pattern.Length;
			var to = this.RvaToOffset(end);
			for (var i = from; i >= to; i--)
			{
				if (Matches(i)) return end + (uint)(i - to) + (uint)offset;
			}
		}

		return 0;
	}

	/// <summary>
	/// Given any RVA inside a function, finds its entry point by scanning backwards
	/// for a PUSH ebp / MOV ebp,esp prologue (0x55 0x8B 0xEC).
	/// </summary>
	public uint FindFunctionStart(uint rva, uint range = 0xff)
		=> this.FindInRange([0x55, 0x8B, 0xEC], "xxx", 0, rva, rva - range);

	private uint FollowCall(uint callRva)
	{
		var posInFile = this.RvaToOffset(callRva);
		var op = this.buffer[posInFile];
		var callParam = BitConverter.ToInt32(this.buffer, posInFile + 1);

		if (op != 0xE8 && op != 0xE9)
			throw new Exception($"Unsupported opcode '0x{op:X2}'");

		return callRva + (uint)callParam + 5;
	}

	public byte ReadByte(uint rva)
		=> this.buffer[this.RvaToOffset(rva)];

	private uint Read(uint rva)
		=> BitConverter.ToUInt32(this.buffer, this.RvaToOffset(rva));

	private int RvaToOffset(uint rva)
	{
		var section = this.peFile.ImageSectionHeaders!.FirstOrDefault(s => rva >= s.VirtualAddress && rva <= s.VirtualAddress + s.VirtualSize);
		if (section == null)
			throw new Exception($"Could not find section for RVA 0x{rva:X8}");
		return (int)(rva - section.VirtualAddress + section.PointerToRawData);
	}
}
