using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Research.Kinect.Nui;
using Coding4Fun.Kinect.Wpf;

namespace Locating_Orange_Kinect
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        Runtime nui;

        #region Initialize

        private void SetupKinect()
        {
            if (Runtime.Kinects.Count == 0)
            {
                this.Title = "No Kinect connected";
            }
            else
            {
                // use the first kinect
                nui = Runtime.Kinects[0];
                nui.Initialize(RuntimeOptions.UseColor);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SetupKinect();
            nui.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_VideoFrameReady);
            nui.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
        }

        #endregion

        void nui_VideoFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            // Convert color information with filter
            byte[] coloredBytes = GenerateColoredBytes(e.ImageFrame);

            // create an image based on returned colors
            PlanarImage image = e.ImageFrame.Image;
            rgbImageContainer.Source = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgr32, null,
                coloredBytes, image.Width * PixelFormats.Bgr32.BitsPerPixel / 8);

            regularImageContainer.Source = e.ImageFrame.ToBitmapSource();
            
        }

        private byte[] GenerateColoredBytes(ImageFrame imageFrame) 
        {
            int height = imageFrame.Image.Height;
            int width = imageFrame.Image.Width;

            // Color Data for each pixel
            Byte[] colorData = imageFrame.Image.Bits;

            // colorFrame contains information for all pixels in image
            // Height x Width x 4 (Red, Green, Blue, empty byte)
            Byte[] colorFrame = new byte[imageFrame.Image.Height * imageFrame.Image.Width * 4];

            // Bgr32 - Blue, Green, Red, empty byte
            // Bgra32 - Blue, Green, Red, transparency
            // You must set transparency for Bgra as .NET defaults a byte to 0 = fully transparent

            // hardcoded locations to Blue, Green, Red (BGR) index positions
            const int BlueIndex = 0;
            const int GreenIndex = 1;
            const int RedIndex = 2;

            // hardcoded thresholds for the colors
            byte[] maxColorThresholds = { 40, 255, 180, 0 }; // Blue, Green, Red, 0
            byte[] minColorThresholds = { 5, 160, 0, 0 };
            
            var colorIndex = 0;

            for (var y = 0; y < height; y++) 
            {
                var heightOffset = y * width;

                for (var x = 0; x < width; x++) 
                {
                    var index = ((width - x - 1) + heightOffset) * 4;
                    byte[] pixelGroup = new byte[4];

                    for (var i = 0; i < 4; i++) 
                    {
                        var color = colorData[colorIndex];
                        // copy 4 bytes to array to determine total rgb color
                        pixelGroup[i] = (byte)color;
                        // moves to next byte
                        colorIndex += 1;
                    }

                    // filters colors
                    byte[] newPixelGroup = filterRGBColorBlack(pixelGroup, minColorThresholds, maxColorThresholds);

                    // adds filtered colors to color frame
                    colorFrame[index + BlueIndex] = newPixelGroup[0];
                    colorFrame[index + GreenIndex] = newPixelGroup[1];
                    colorFrame[index + RedIndex] = newPixelGroup[2];
                    colorFrame[index + 3] = newPixelGroup[3];
                }
            }

            return colorFrame;
        }

        private byte[] filterRGBColorBlack(byte[] inputColors, byte[] minThresholds, byte[] maxThresholds) 
        {
            byte[] newPixelGroup = new byte[4];
            Boolean[] pixelsWithinThreshold = new Boolean[4];
            Boolean overallColorWithinThreshold = true;

            // loops to find result for each byte
            for (var bn = 0; bn < 3; bn++) 
            {
                Boolean isOfLowerThreshold = false;
                Boolean isOfHigherThreshold = false;

                if (inputColors[bn] > minThresholds[bn])
                {
                    isOfLowerThreshold = true;
                }

                if (inputColors[bn] < maxThresholds[bn]) 
                {
                    isOfHigherThreshold = true;
                }

                if (isOfLowerThreshold && isOfHigherThreshold) // If within threshold...
                {
                    pixelsWithinThreshold[bn] = true;
                    newPixelGroup[bn] = inputColors[bn];
                }
                else 
                {
                    pixelsWithinThreshold[bn] = false;
                    newPixelGroup[bn] = 0;
                }
            }
            // last channel is default
            newPixelGroup[3] = 0;
            pixelsWithinThreshold[3] = true;

            // if all are within threshold, return color. Else, color is black
            for (var bn = 0; bn < 4; bn++) 
            {
                if (!pixelsWithinThreshold[bn]) 
                {
                    overallColorWithinThreshold = false;
                }
            }

            // returns the correct values
            if (overallColorWithinThreshold)
            {
                return newPixelGroup;
            }
            else 
            {
                for (var bn = 0; bn < 4; bn++) 
                {
                    newPixelGroup[bn] = 0;
                }
                return newPixelGroup;
            }
        }

        #region Uninitialize

        private void Window_Closed(object sender, EventArgs e)
        {
            nui.Uninitialize();
        }


        #endregion

    }
}
