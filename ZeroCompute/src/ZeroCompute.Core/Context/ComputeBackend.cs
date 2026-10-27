namespace ZeroCompute.Core.Context
{
    public enum ComputeBackend
    {
        CpuParallel = 0,
        Direct3D11 = 1
    }

    public enum ComputeActivationType
    {
        ReLU = 0,
        LeakyReLU = 1,
        GELU = 2,
        Sigmoid = 3,
        Tanh = 4,
        Softmax = 5,
        SiLU = 6,
        None = 99
    }
}
