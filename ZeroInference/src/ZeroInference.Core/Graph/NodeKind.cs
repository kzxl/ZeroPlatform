namespace ZeroInference.Core.Graph
{
    public enum NodeKind
    {
        Input,
        Conv2D,
        FusedConvRelu,
        BatchNormalization,
        Relu,
        LeakyRelu,
        Sigmoid,
        Tanh,
        Gelu,
        MaxPool2D,
        AvgPool2D,
        GlobalAvgPool2D,
        Linear,
        FusedLinearRelu,
        Add,
        Flatten,
        Softmax,
        MultiHeadAttention
    }
}
