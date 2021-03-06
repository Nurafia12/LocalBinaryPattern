﻿using System;
using System.Linq;
using System.Threading.Tasks;

using Accord.Math;


namespace LBPLibrary
{
    /// <summary>
    /// LBP application class object.
    /// Used to calculate Median Robust Extended Local Binary Pattern or LBP from images.
    /// </summary>
    public class LBPApplication
    {   

        /// <summary>
        /// Image to calculate LBP from
        /// </summary>
        public double[,] Image { get; set; }
        /// <summary>
        /// LBP parameters. See Parameters class.
        /// </summary>
        public Parameters Param { get; set; }
        /// <summary>
        /// Option to use Median Robust Extended LBP.
        /// </summary>
        public bool MRE { get; set; }
        /// <summary>
        /// image = array resulting from median filter.
        /// LBP... = LBP image
        /// LBP...Mapped = Rotation invariant uniform mapped LBP image.
        /// </summary>
        public double[,] imageCenter, imageLarge, imageSmall,
            LBPIL, LBPIS, LBPIR,
            LBPILMapped, LBPISMapped, LBPIRMapped;
        /// <summary>
        /// Feature histograms from MRELBP
        /// </summary>
        public int[] histL, histS, histR, histCenter;
        /// <summary>
        /// Image size and d = LBP radius.
        /// </summary>
        public int xSize, ySize, d;
        /// <summary>
        /// Table for rotation invariant uniform mapping.
        /// </summary>
        public int[] mappingTable;

        /// <summary>
        /// Calculates LBP from 2D array
        /// Enter array, radius and neighbours.
        /// </summary>
        public static void PipelineLBP(double[,] image, Parameters param, 
            out double[,] result, out int[] histogram)
        {   

            // Create instance of LBPApplication and set input variables
            LBPApplication LBPapp = new LBPApplication
            {
                Image = image,
                Param = param,
                MRE = false // Don't use MRELBP methods
            };

            // Result image size
            LBPapp.d = LBPapp.Param.Radius;
            LBPapp.xSize = image.GetLength(0) - 2 * LBPapp.d;
            LBPapp.ySize = image.GetLength(1) - 2 * LBPapp.d;
            
            // Calculate mapping and LBP image
            LBPapp.GetMapping();
            LBPapp.CalculateImage();

            // Calculate histogram
            LBPapp.GetHistogram();

            // output variables
            result = LBPapp.LBPISMapped; // Mapping
            histogram = LBPapp.histS;
        }

        /// <summary>
        /// Calculates MRELBP from 2D array
        /// Enter array and parameters class including small and large radius, neighbours and kernel weights.
        /// Weights are for center pixels (w_c), small (w_r[0]) and large (w_r[1]) radius.
        /// </summary>
        public static void PipelineMRELBP(double[,] image, Parameters param,
            out double[,] LBPIL, out double[,] LBPIS, out double[,] LBPIR, out int[] histL, out int[] histS, out int[] histR, out int[] histCenter)
        {   

            // Create instance of LBPApplication and set input variables
            LBPApplication MRELBPapp = new LBPApplication
            {
                Image = image,
                Param = param,
                d = param.LargeRadius + (param.W_r[0] - 1) / 2,
                MRE = true // Median robust extended LBP
            };

            // Calculate mapping
            MRELBPapp.GetMapping();

            // Result image size
            MRELBPapp.xSize = image.GetLength(0) - 2 * MRELBPapp.d;
            MRELBPapp.ySize = image.GetLength(1) - 2 * MRELBPapp.d;

            // Scale image
            MRELBPapp.Scaling();

            // Median filtering using given weights
            MRELBPapp.FilterImage();

            // Calculate LBP image
            MRELBPapp.CalculateImage();

            // Calculate histogram
            MRELBPapp.GetHistogram();

            // output variables
            //LBPIL = MRELBPapp.LBPILMapped; // Mapping
            //LBPIS = MRELBPapp.LBPISMapped;
            //LBPIR = MRELBPapp.LBPIRMapped;
            LBPIL = MRELBPapp.LBPIL; // No mapping
            LBPIS = MRELBPapp.LBPIS;
            LBPIR = MRELBPapp.LBPIR;


            histL = MRELBPapp.histL;
            histS = MRELBPapp.histS;
            histR = MRELBPapp.histR;
            histCenter = MRELBPapp.histCenter;
        }

        /// <summary>
        /// Scales array by mean and standard deviation.
        /// </summary>
        public void Scaling()
        {
            // Calculate mean and standard deviation
            double mean = Functions.Mean(Image);
            double std = Functions.Std(Image);
            if (std != 0)
                Image = Image.Subtract(mean).Divide(std);
            else // Check for division by 0
                throw new Exception("Standard deviation of the image is 0! Cannot divide!");
        }

        /// <summary>
        /// Performs median filtering for images and calculates center histogram.
        /// </summary>
        public void FilterImage()
        {
            // Median filter image using different kernels
            MedianFilter mc = new MedianFilter(Param.W_c);
            imageCenter = mc.Filtering(Image);
            MedianFilter ml = new MedianFilter(Param.W_r[0]);
            imageLarge = ml.Filtering(Image);
            MedianFilter ms = new MedianFilter(Param.W_r[1]);
            imageSmall = ms.Filtering(Image);

            // Subtract mean from cropped center image (no edge artefacts)
            double[,] imageCrop = Functions.GetSubMatrix(imageCenter, d, xSize + d - 1, d, ySize + d - 1);
            imageCenter = imageCenter
                .Subtract(
                Functions.Mean(imageCrop));

            // Calculate center pixel histogram
            histCenter = new int[2];
            for (int x = d; x < xSize + d; x++)
            {
                for (int y = d; y < ySize + d; y++)
                {
                    if (imageCenter[x, y] >= -Param.Eps1)
                        histCenter[0]++;
                    else
                        histCenter[1]++;
                }
            }
        }

        /// <summary>
        /// Calculates the actual LBP image
        /// </summary>
        public void CalculateImage()
        {   
            // Initialize result arrays
            LBPILMapped = new double[xSize, ySize];
            LBPIRMapped = new double[xSize, ySize];
            LBPISMapped = new double[xSize, ySize];
            LBPIL = new double[xSize, ySize];
            LBPIS = new double[xSize, ySize];
            LBPIR = new double[xSize, ySize];

            // Get angles for neighbours
            double angle = - 2 * Math.PI / Param.Neighbours;

            // LBP is calculated for every pixel individually
            //Parallel for-loop for every pixel[i, j]
            Parallel.For(d, xSize + d, i =>
            {
                Parallel.For(d, ySize + d, j =>
                {
                    // Declare local variables for each pixel [i, j]
                    double[] NL = new double[Param.Neighbours],
                             NS = new double[Param.Neighbours],
                             NR = new double[Param.Neighbours];
                    int[] compS = new int[Param.Neighbours],
                          compL = new int[Param.Neighbours],
                          compR = new int[Param.Neighbours];

                    // Calculate neighbours
                    for (int k = 0; k < Param.Neighbours; k++)
                    {
                        // Declare local variables for each neighbour
                        int rX, rY, rx, ry;
                        double x, y, X, Y;
                        if (MRE)
                        {
                            x = Param.Radius * Math.Cos(k * angle); // Radius is taken to account at Parallel.For
                            y = Param.Radius * Math.Sin(k * angle);
                            X = Param.LargeRadius * Math.Cos(k * angle);
                            Y = Param.LargeRadius * Math.Sin(k * angle);
                        }
                        else
                        {
                            x = Param.Radius * Math.Cos(k * angle); // Radius is taken to account at Parallel.For
                            y = Param.Radius * Math.Sin(k * angle);
                            X = 0; Y = 0; // unused
                        }

                        // Round x and y
                        rx = (int)Math.Round(x);
                        ry = (int)Math.Round(y);
                        rX = (int)Math.Round(X);
                        rY = (int)Math.Round(Y);

                        // Calculate neighbour values

                        // LBP and small neighbourhood
                        if ((Math.Abs(x - rx) < Param.Eps1) && (Math.Abs(y - ry) < Param.Eps1)) // Interpolation not necessary
                        {
                            if (MRE)
                                NS[k] = imageSmall[i + rx, j + ry]; // MRELBP
                            else
                                NS[k] = Image[i + rx, j + ry]; // LBP
                        }
                        else // Bilinear interpolation
                        {
                            // Create Neighbour from interpolated values
                            if (MRE)
                                NS[k] = Functions.Bilinear(imageSmall, x, y, i, j, Param.Eps2);
                            else
                                NS[k] = Functions.Bilinear(Image, x, y, i, j, Param.Eps2);
                        }

                        // Large and radial neighbourhood
                        if (MRE)
                        {
                            // Large neighbourhood
                            if ((Math.Abs(X - rX) < Param.Eps1) && (Math.Abs(Y - rY) < Param.Eps1)) // Interpolation not necessary
                                NL[k] = imageLarge[i + rX, j + rY]; // small radius and LBP
                            else // Bilinear interpolation
                                NL[k] = Functions.Bilinear(imageLarge, X, Y, i, j, Param.Eps2);

                            // Radial neighbourhood thresholding (subtract small from large radius)
                            NR[k] = NL[k] - NS[k];
                        }
                    }

                    // Compare values
                    if (MRE)
                    {
                        // Calculate means
                        double NLmean = Functions.Mean(NL.ToMatrix()), NSmean = Functions.Mean(NS.ToMatrix());

                        for (int k = 0; k < Param.Neighbours; k++)
                        {
                            // Subtract mean neighbour values
                            NL[k] = NL[k] - NLmean;
                            NS[k] = NS[k] - NSmean;

                            // Check for positive values
                            if (NS[k] >= -Param.Eps1) // NOTE ACCURACY FOR THRESHOLDING!!!
                                compS[k] = 1;
                            if (NL[k] >= -Param.Eps1)
                                compL[k] = 1;
                            if (NR[k] >= -Param.Eps1)
                                compR[k] = 1;
                        }
                    }
                    else
                    {
                        // Comparison of neighbours
                        compS = new int[Param.Neighbours];
                        for (int k = 0; k < Param.Neighbours; k++)
                        {   // Compare neighbour to center values, creating binary mask
                            if (NS[k] >= Image[i, j] - Param.Eps1) // NOTE ACCURACY FOR THRESHOLDING!!!
                                compS[k] = 1;
                        }
                    }

                    // Sum neighbours into LBP values
                    int value;
                    for (int k = 0; k < Param.Neighbours; k++)
                    {
                        value = (int)Math.Pow(2, k); // binary label
                        LBPIL[i - d, j - d] = LBPIL[i - d, j - d] + compL[k] * value;
                        LBPIR[i - d, j - d] = LBPIR[i - d, j - d] + compR[k] * value;
                        LBPIS[i - d, j - d] = LBPIS[i - d, j - d] + compS[k] * value;
                    }

                    // Apply mapping
                    LBPILMapped[i - d, j - d] = mappingTable[(int)LBPIL[i - d, j - d]];
                    LBPIRMapped[i - d, j - d] = mappingTable[(int)LBPIR[i - d, j - d]];
                    LBPISMapped[i - d, j - d] = mappingTable[(int)LBPIS[i - d, j - d]];
                });
            });
        }

        /// <summary>
        /// Gets rotation invariant uniform mapping for neighbours
        /// </summary>
        public void GetMapping()
        {   
            int n = Param.Neighbours, length = (int)Math.Pow(2, Param.Neighbours), newmax = n + 2;
            mappingTable = new int[length];

            // Loop for mapping
            for (int i = 0; i < length; i++)
            {
                // Binary and shifted binary strings (rotation invariance)
                string binary = Convert.ToString(i, 2).PadLeft(n, '0'),
                       binaryShift = binary.Substring(1, n - 1) + binary.Substring(0, 1);

                // Convert strings to string arrays
                int sum = 0, c = 0;
                for (int ii = 0; ii < n; ii++)
                {
                    string bit = binary.Substring(ii, 1),
                           bitShift = binaryShift.Substring(ii, 1);
                    if (bit != bitShift) // Calculate sum of different bits (uniformity)
                        sum++;
                    c +=Convert.ToInt32(bit); // Sum of original bits
                }

                // Binning
                if (sum <= 2)
                    mappingTable[i] = c;
                else
                    mappingTable[i] = Param.Neighbours + 1;
            }
        
        }

        /// <summary>
        /// Gets histogram from mapped images
        /// </summary>
        public void GetHistogram()
        {   
            int length = mappingTable.Max() + 1;
            histS = new int[length];

            if (MRE)
            {
                // Calculate the histogram
                histL = new int[length];
                histR = new int[length];
                for (int k = 0; k < length; k++)
                {
                    for (int i = 0; i < xSize; i++)
                    {
                        for (int j = 0; j < ySize; j++)
                        {
                            if (LBPILMapped[i, j] == k)
                                histL[k]++;
                            if (LBPISMapped[i, j] == k)
                                histS[k]++;
                            if (LBPIRMapped[i, j] == k)
                                histR[k]++;
                        }
                    }
                }
            }
            else
            {
                // Calculate the histogram
                for (int k = 0; k < length; k++)
                {
                    for (int i = 0; i < xSize; i++)
                    {
                        for (int j = 0; j < ySize; j++)
                        {
                            if (LBPISMapped[i, j] == k)
                                histS[k]++;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Performs median filtering on given arrays.
    /// Kernel size has to be given.
    /// </summary>
    public class MedianFilter
    {   
        /// <summary>
        /// Kernel width and distance (can be calculated from width)
        /// </summary>
        public int kernel, distance;

        /// <summary>
        /// Initializes default input values for median filter.
        /// Kernel width = 5, distance = 2
        /// </summary>
        public MedianFilter()
        {   // Default parameters
            kernel = 5;
            distance = (kernel - 1) / 2;
        }

        /// <summary>
        /// Override for user defined kernel width.
        /// </summary>
        public MedianFilter(int kernel)
        {
            this.kernel = kernel;
            distance = (kernel - 1) / 2;
        }

        /// <summary>
        /// Calculate median filter from array 
        /// </summary>
        public double[,] Filtering(double[,] array)
        {   // Calculates median image using given (odd) kernel. 
            int w = array.GetLength(0), l = array.GetLength(1);

            if (kernel % 2 == 0) // check for odd kernel
                throw new Exception("Kernel width is not odd!");
            if (distance > w || distance > l)
                throw new Exception("Kernel radius is larger than input array!");

            double[,] paddedArray = Functions.ArrayPadding(array, distance, ""); // Zero padding enough when image is cropped
            double[,] imageFiltered = new double[w, l];

            Parallel.For(0, w, i =>
            {
                Parallel.For(0, l, j =>
                {   // Calculate median from the block pixel by pixel
                    double[] block = Functions.ArrayToVector(
                        Functions.GetSubMatrix(paddedArray, i, i + 2 * distance, j, j + 2 * distance));
                    Array.Sort(block);
                    imageFiltered[i, j] = block[(int)Math.Floor((double)kernel * kernel / 2)];
                });
            });
            return imageFiltered;
        }
    }

    /// <summary>
    /// Performs grayscale standardization for images.
    /// Standardizes by gaussian mean with given gernels and std:s.
    /// </summary>
    public class LocalStandardization
    {   
        /// <summary>
        /// Parameter for standardization.
        /// w = kernel width, s = sigma (variance)
        /// </summary>
        public int w1, w2, s1, s2;

        /// <summary>
        /// Initializes default input values for Grayscale standardization.
        /// w1 = 23, w2 = 5, s1 = 5, s2 = 1.
        /// </summary>
        public LocalStandardization()
        {   // Default parameters
            w1 = 23; w2 = 5;
            s1 = 5; s2 = 1;
        }

        /// <summary>
        /// Override for user defined standardization parameters.
        /// </summary>
        public LocalStandardization(int weight1, int weight2, int sigma1, int sigma2)
        {   // Input parameters
            w1 = weight1; w2 = weight2;
            s1 = sigma1; s2 = sigma2;
        }

        /// <summary>
        /// Method to standardize image grayscale values using previously defined inputs for gaussian kernels.
        /// Method string defines padding method.
        /// </summary>
        public void Standardize(ref double[,] image, string method)
        {
            // Get Gaussian kernels
            double[,] kernel1 = GaussianKernel(w1, s1),
                      kernel2 = GaussianKernel(w2, s2);

            // Blurring with kernels
            double[,] blurredImage1 = Functions.Convolution2D(kernel1, image, method),
                      blurredImage2 = Functions.Convolution2D(kernel2, image, method); // Alternative blurring, not used currently

            // Centering
            double[,] centered = image.Subtract(blurredImage1);

            // Standardization
            double[,] std = Functions.Convolution2D(kernel2, centered.Pow(2), method).Sqrt();
            image = centered // This function standardizes and modifies original given image
                .Divide(
                std.Add(1E-09));
        }

        /// <summary>
        /// Give kernel width w and sigma s to calculate Gaussian kernel.
        /// </summary>
        public double[,] GaussianKernel(int w, int s)
        {
            if (w % 2 == 0) // Check for odd kernel
                throw new Exception("Kernel width is not odd!");

            double[,] kernel = new double[w, w];
            // Constant for centering
            double d = (w - 1) / 2;
            Parallel.For(0, w, i =>
            {
                Parallel.For(0, w, j =>
                {
                    double x = -(Math.Pow(i - d, 2) + Math.Pow(j - d, 2)) / (2 * Math.Pow(s, 2));
                    kernel[i, j] = Math.Exp(x);
                });
            });

            // Normalization
            double sum = kernel.Sum();
            kernel = kernel.Divide(sum);

            return kernel;
        }
    }
}
