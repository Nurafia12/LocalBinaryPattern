﻿using System;
using LBPLibrary;
using LBP.UnitTests;
using Accord.Math;
using Xunit;

namespace LBP.UnitTests
{
    public class ConvolutionTests
    {
        TestImage testImg = new TestImage(); // Initialize testimage function
        double[,] kernel;

        [Xunit.Theory]
        [InlineData("Ones", "Nearest")] // Test different kernel widths, image patterns and paddings
        [InlineData("Ones", "Reflect")]
        [InlineData("", "Nearest")]
        [InlineData("", "Reflect")]
        [InlineData("Quarters", "Nearest")]
        [InlineData("Quarters", "Reflect")]
        [InlineData("Running numbers", "Nearest")]
        [InlineData("Running numbers", "Reflect")]
        public void Convolute_TestArrayOneKernel_EqualsInputArray(string pattern, string paddingMethod)
        {
            // Image
            testImg.New(pattern, new int[] { 30, 30 });

            for (int w = 1; w < 21; w+=4)
            {
                // Kernel
                kernel = new double[w, w];
                double d = (w - 1) / 2;
                kernel[(int)Math.Floor(d), (int)Math.Floor(d)] = 1;

                double[,] convResult = Functions.Convolution2D(kernel, testImg.Image.ToDouble(), paddingMethod);

                Assert.Equal(testImg.Image.ToDouble(), convResult);
            }
        }

        [Theory]
        [InlineData("Ones", "Nearest")] // Test different kernel widths, image patterns and paddings
        [InlineData("Ones", "Reflect")]
        [InlineData("", "Nearest")]
        [InlineData("", "Reflect")]
        [InlineData("Quarters", "Nearest")]
        [InlineData("Quarters", "Reflect")]
        [InlineData("Running numbers", "Nearest")]
        [InlineData("Running numbers", "Reflect")]
        public void Convolute_TestArrayWithModifiedKernel_EqualsInputArray(string pattern, string paddingMethod)
        {
            // Arrange
            testImg.New(pattern);
            for (int k = 0; k < 100; k+=10)
            {
                float e = (float)(0 + k * 0.000000001);

                // Random kernel
                int w = 5;
                kernel = new double[w, w];
                Random r = new Random();
                for (int i = 0; i < w; i++)
                {
                    for (int j = 0; j < w; j++)
                    {
                        kernel[i, j] = r.Next(0, 1000);
                    }
                }
                double[,] kernel2 = kernel.Add(e); // Second kernel with residual
                
                // Act
                double[,] convResult = Functions.Convolution2D(kernel, testImg.Image.ToDouble(), paddingMethod);
                double[,] convResult2 = Functions.Convolution2D(kernel2, testImg.Image.ToDouble(), paddingMethod);

                // Assert
                for (int i = 0; i < convResult.GetLength(0); i++)
                {
                    for (int j = 0; j < convResult.GetLength(1); j++)
                    {
                         Assert.Equal(convResult[i, j], convResult2[i, j], 4);
                    }
                }
            }
        }

        [Theory]
        [InlineData("Quarters", "Reflect")]
        public void Convolute_SmallQuarter_EqualsPythonArray(string pattern, string paddingMethod)
        {   /// Test whether convolution2D function equals to scipy.ndimage.convolve (default)
            testImg.New(pattern, new int[] { 6, 6 });
            int w = testImg.Image.GetLength(0), l = testImg.Image.GetLength(1);
            
            // Convolute
            double[,] kernel = new double[9, 9].Add(1); // Ones kernel
            double[,] convolution = Functions.Convolution2D(kernel, testImg.Image.ToDouble(), paddingMethod);

            float[,] refArray = new float[6, 6] // Here, actually columns are written out
                {{ 162, 162, 180, 198, 216, 216},
                { 162, 162, 180, 198, 216, 216},
                { 171, 171, 189, 207, 225, 225},
                { 180, 180, 198, 216, 234, 234},
                { 189, 189, 207, 225, 243, 243},
                { 189, 189, 207, 225, 243, 243} };
            Assert.Equal(refArray.ToDouble(), convolution);
        }

        [Fact]
        public void Convolute_LargeOrOddkernel_ThrowsCorrectException()
        {   /// Test whether Medianfilter class throws correct exceptions
            testImg.New("Quarters", new int[] { 6, 6 });

            // Filter
            double[,] kernelLarge = new double[15, 15].Add(1); // Ones kernel
            double[,] kernelEven = new double[2, 2].Add(1); // Ones kernel

            Exception ex = Assert.Throws<Exception>(
                delegate { Functions.Convolution2D(kernelLarge, testImg.Image.ToDouble(), "Nearest"); });
            Assert.Equal("Kernel radius is larger than input array!", ex.Message);
            Exception ex2 = Assert.Throws<Exception>(
                delegate { Functions.Convolution2D(kernelEven, testImg.Image.ToDouble(), "Nearest"); });
            Assert.Equal("Kernel width is not odd!", ex2.Message);
        }
    }
}
