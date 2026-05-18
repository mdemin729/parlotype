namespace Parlotype.Core.LlamaServer;

/// <summary>CPU architecture a llama.cpp server build targets.</summary>
public enum LlamaServerArch
{
    Unknown = 0,
    X64,
    Arm64,
}
