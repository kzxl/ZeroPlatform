using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class TensorSerializationAndAdvancedOpsTests
    {
        #region TopK Tests

        [Fact]
        public void TopK_1D_Float_LargestAndSmallest()
        {
            var t = Tensor.FromArray(new float[] { 10f, 50f, 20f, 40f, 30f });

            // Top 3 largest
            var (topValues, topIndices) = TensorOps.TopK(t, k: 3, axis: 0, largest: true);
            Assert.Equal(new[] { 3 }, topValues.Shape.Dimensions);
            Assert.Equal(50f, topValues[0]);
            Assert.Equal(40f, topValues[1]);
            Assert.Equal(30f, topValues[2]);

            Assert.Equal(1, topIndices[0]);
            Assert.Equal(3, topIndices[1]);
            Assert.Equal(4, topIndices[2]);

            // Top 2 smallest
            var (botValues, botIndices) = TensorOps.TopK(t, k: 2, axis: 0, largest: false);
            Assert.Equal(10f, botValues[0]);
            Assert.Equal(20f, botValues[1]);
            Assert.Equal(0, botIndices[0]);
            Assert.Equal(2, botIndices[1]);
        }

        [Fact]
        public void TopK_2D_Float_AlongAxis()
        {
            // [ [1, 5, 3],
            //   [8, 2, 6] ]
            var t = Tensor.FromArray(new float[]
            {
                1f, 5f, 3f,
                8f, 2f, 6f
            }, 2, 3);

            // Along axis 1 (across cols), k=2 largest
            var (vals, idxs) = TensorOps.TopK(t, k: 2, axis: 1, largest: true);
            Assert.Equal(new[] { 2, 2 }, vals.Shape.Dimensions);

            // Row 0: largest are 5 (idx 1), 3 (idx 2)
            Assert.Equal(5f, vals[0, 0]);
            Assert.Equal(1, idxs[0, 0]);
            Assert.Equal(3f, vals[0, 1]);
            Assert.Equal(2, idxs[0, 1]);

            // Row 1: largest are 8 (idx 0), 6 (idx 2)
            Assert.Equal(8f, vals[1, 0]);
            Assert.Equal(0, idxs[1, 0]);
            Assert.Equal(6f, vals[1, 1]);
            Assert.Equal(2, idxs[1, 1]);
        }

        #endregion

        #region Gather Tests

        [Fact]
        public void Gather_2D_MatchesAnalyticalValues()
        {
            // Input:
            // [ [10, 20, 30],
            //   [40, 50, 60] ]
            var input = Tensor.FromArray(new float[]
            {
                10f, 20f, 30f,
                40f, 50f, 60f
            }, 2, 3);

            // Indices for axis 1:
            // [ [2, 0],
            //   [1, 2] ]
            var indices = Tensor.FromArray(new int[]
            {
                2, 0,
                1, 2
            }, 2, 2);

            var gathered = TensorOps.Gather(input, axis: 1, indices: indices);
            Assert.Equal(new[] { 2, 2 }, gathered.Shape.Dimensions);

            // Row 0: input[0, 2] = 30, input[0, 0] = 10
            Assert.Equal(30f, gathered[0, 0]);
            Assert.Equal(10f, gathered[0, 1]);

            // Row 1: input[1, 1] = 50, input[1, 2] = 60
            Assert.Equal(50f, gathered[1, 0]);
            Assert.Equal(60f, gathered[1, 1]);
        }

        #endregion

        #region CumSum Tests

        [Fact]
        public void CumSum_FloatAndDouble()
        {
            var t = Tensor.FromArray(new float[]
            {
                1f, 2f, 3f,
                4f, 5f, 6f
            }, 2, 3);

            // Axis 0: running sum across rows
            var cs0 = TensorOps.CumSum(t, axis: 0);
            Assert.Equal(1f, cs0[0, 0]);
            Assert.Equal(2f, cs0[0, 1]);
            Assert.Equal(3f, cs0[0, 2]);
            Assert.Equal(5f, cs0[1, 0]); // 1 + 4
            Assert.Equal(7f, cs0[1, 1]); // 2 + 5
            Assert.Equal(9f, cs0[1, 2]); // 3 + 6

            // Axis 1: running sum across cols
            var cs1 = TensorOps.CumSum(t, axis: 1);
            Assert.Equal(1f, cs1[0, 0]);
            Assert.Equal(3f, cs1[0, 1]); // 1 + 2
            Assert.Equal(6f, cs1[0, 2]); // 1 + 2 + 3

            // Double
            var td = Tensor.FromArray(new double[] { 10.0, 20.0, 30.0 }, 3);
            var csd = TensorOps.CumSum(td, axis: 0);
            Assert.Equal(10.0, csd[0]);
            Assert.Equal(30.0, csd[1]);
            Assert.Equal(60.0, csd[2]);
        }

        #endregion

        #region OneHot Tests

        [Fact]
        public void OneHot_GeneratesCorrectEncoding()
        {
            var indices = Tensor.FromArray(new int[] { 0, 2, 1 }, 3);
            var oneHot = Tensor.OneHot(indices, depth: 3);

            Assert.Equal(new[] { 3, 3 }, oneHot.Shape.Dimensions);

            // Row 0: class 0 -> [1, 0, 0]
            Assert.Equal(1f, oneHot[0, 0]);
            Assert.Equal(0f, oneHot[0, 1]);
            Assert.Equal(0f, oneHot[0, 2]);

            // Row 1: class 2 -> [0, 0, 1]
            Assert.Equal(0f, oneHot[1, 0]);
            Assert.Equal(0f, oneHot[1, 1]);
            Assert.Equal(1f, oneHot[1, 2]);

            // Row 2: class 1 -> [0, 1, 0]
            Assert.Equal(0f, oneHot[2, 0]);
            Assert.Equal(1f, oneHot[2, 1]);
            Assert.Equal(0f, oneHot[2, 2]);
        }

        #endregion

        #region Double ArgMax and ArgMin Tests

        [Fact]
        public void Double_ArgMax_And_ArgMin()
        {
            var t = Tensor.FromArray(new double[]
            {
                1.0, 9.0, 3.0,
                8.0, 2.0, 7.0
            }, 2, 3);

            var argMax = TensorOps.ArgMax(t, axis: 1);
            Assert.Equal(1, argMax[0]); // 9.0 at index 1
            Assert.Equal(0, argMax[1]); // 8.0 at index 0

            var argMin = TensorOps.ArgMin(t, axis: 1);
            Assert.Equal(0, argMin[0]); // 1.0 at index 0
            Assert.Equal(1, argMin[1]); // 2.0 at index 1
        }

        #endregion

        #region Npy Serialization Tests

        [Fact]
        public void Npy_RoundTrip_Float_Double_Int()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZeroTensor_Npy_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // 1. Float 2D
                var fTensor = Tensor.FromArray(new float[]
                {
                    1.5f, 2.5f, 3.5f,
                    4.5f, 5.5f, 6.5f
                }, 2, 3);
                string fPath = Path.Combine(tempDir, "test_float.npy");
                fTensor.SaveNpy(fPath);

                var loadedFloat = Tensor.LoadNpy<float>(fPath);
                Assert.Equal(fTensor.Shape.Dimensions, loadedFloat.Shape.Dimensions);
                for (int r = 0; r < 2; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        Assert.Equal(fTensor[r, c], loadedFloat[r, c]);
                    }
                }

                // 2. Double 1D
                var dTensor = Tensor.FromArray(new double[] { 3.1415926535, 2.7182818284 });
                string dPath = Path.Combine(tempDir, "test_double.npy");
                dTensor.SaveNpy(dPath);

                var loadedDouble = Tensor.LoadNpy<double>(dPath);
                Assert.Equal(dTensor.Shape.Dimensions, loadedDouble.Shape.Dimensions);
                Assert.Equal(dTensor[0], loadedDouble[0]);
                Assert.Equal(dTensor[1], loadedDouble[1]);

                // 3. Int 3D
                var iTensor = new Tensor<int>(2, 2, 2);
                iTensor[0, 0, 0] = 100;
                iTensor[1, 1, 1] = 999;
                string iPath = Path.Combine(tempDir, "test_int.npy");
                iTensor.SaveNpy(iPath);

                var loadedInt = Tensor.LoadNpy<int>(iPath);
                Assert.Equal(iTensor.Shape.Dimensions, loadedInt.Shape.Dimensions);
                Assert.Equal(100, loadedInt[0, 0, 0]);
                Assert.Equal(999, loadedInt[1, 1, 1]);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }

        #endregion

        #region Safetensors Serialization Tests

        [Fact]
        public void Safetensors_RoundTrip_MultipleTensors()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZeroTensor_Safetensors_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dict = new Dictionary<string, Tensor<float>>
                {
                    ["model.embed_tokens.weight"] = Tensor.FromArray(new float[]
                    {
                        0.1f, 0.2f, 0.3f,
                        0.4f, 0.5f, 0.6f
                    }, 2, 3),

                    ["model.layers.0.self_attn.q_proj.bias"] = Tensor.FromArray(new float[]
                    {
                        10f, 20f, 30f
                    }, 3)
                };

                string sfPath = Path.Combine(tempDir, "model.safetensors");
                SafetensorsFile.Save(sfPath, dict);

                var loaded = SafetensorsFile.Load(sfPath);
                Assert.Equal(2, loaded.Count);
                Assert.True(loaded.ContainsKey("model.embed_tokens.weight"));
                Assert.True(loaded.ContainsKey("model.layers.0.self_attn.q_proj.bias"));

                var loadedW = loaded["model.embed_tokens.weight"];
                Assert.Equal(new[] { 2, 3 }, loadedW.Shape.Dimensions);
                Assert.Equal(0.1f, loadedW[0, 0]);
                Assert.Equal(0.6f, loadedW[1, 2]);

                var loadedB = loaded["model.layers.0.self_attn.q_proj.bias"];
                Assert.Equal(new[] { 3 }, loadedB.Shape.Dimensions);
                Assert.Equal(10f, loadedB[0]);
                Assert.Equal(20f, loadedB[1]);
                Assert.Equal(30f, loadedB[2]);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }

        #endregion

        #region Comparison and Where Tests

        [Fact]
        public void Comparisons_And_Where_OperateCorrectly()
        {
            var a = Tensor.FromArray(new float[] { 1f, 5f, 10f, 2f }, 2, 2);
            var b = Tensor.FromArray(new float[] { 2f, 3f, 10f, 8f }, 2, 2);

            // GreaterThan: [ [false, true], [false, false] ]
            var gt = TensorOps.GreaterThan(a, b);
            Assert.False(gt[0, 0]);
            Assert.True(gt[0, 1]);
            Assert.False(gt[1, 0]);
            Assert.False(gt[1, 1]);

            // Scalar GreaterThan: a > 3f -> [ [false, true], [true, false] ]
            var gtScalar = TensorOps.GreaterThan(a, 3f);
            Assert.False(gtScalar[0, 0]);
            Assert.True(gtScalar[0, 1]);
            Assert.True(gtScalar[1, 0]);
            Assert.False(gtScalar[1, 1]);

            // Where(gtScalar, a, 0f) -> [ [0, 5], [10, 0] ]
            var filtered = Tensor.Where(gtScalar, a, 0f);
            Assert.Equal(0f, filtered[0, 0]);
            Assert.Equal(5f, filtered[0, 1]);
            Assert.Equal(10f, filtered[1, 0]);
            Assert.Equal(0f, filtered[1, 1]);

            // Mask(a > 3f) -> [ 5, 10 ]
            var masked = a.Mask(gtScalar);
            Assert.Equal(new[] { 2 }, masked.Shape.Dimensions);
            Assert.Equal(5f, masked[0]);
            Assert.Equal(10f, masked[1]);

            // Any & All
            Assert.True(Tensor.Any(gtScalar));
            Assert.False(Tensor.All(gtScalar));

            var allTrue = new Tensor<bool>(2, 2);
            allTrue.Fill(true);
            Assert.True(Tensor.All(allTrue));
        }

        [Fact]
        public void Generic_Eye_And_Double_Arange_Linspace()
        {
            // Generic Eye<double> 3x4
            var eyeDouble = Tensor.Eye<double>(3, 4);
            Assert.Equal(new[] { 3, 4 }, eyeDouble.Shape.Dimensions);
            Assert.Equal(1.0, eyeDouble[0, 0]);
            Assert.Equal(1.0, eyeDouble[1, 1]);
            Assert.Equal(1.0, eyeDouble[2, 2]);
            Assert.Equal(0.0, eyeDouble[0, 1]);
            Assert.Equal(0.0, eyeDouble[2, 3]);

            // Double Arange
            var arange = Tensor.Arange(1.0, 4.0, 1.0);
            Assert.Equal(new[] { 3 }, arange.Shape.Dimensions);
            Assert.Equal(1.0, arange[0]);
            Assert.Equal(2.0, arange[1]);
            Assert.Equal(3.0, arange[2]);

            // Double Linspace
            var linspace = Tensor.Linspace(0.0, 10.0, 5);
            Assert.Equal(new[] { 5 }, linspace.Shape.Dimensions);
            Assert.Equal(0.0, linspace[0]);
            Assert.Equal(2.5, linspace[1]);
            Assert.Equal(5.0, linspace[2]);
            Assert.Equal(7.5, linspace[3]);
            Assert.Equal(10.0, linspace[4]);
        }

        #endregion
    }
}
