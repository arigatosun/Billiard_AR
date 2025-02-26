#if !UNITY_WSA_10_0

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UtilsModule;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OpenCVForUnity.DnnModule
{
    // C++: class Image2BlobParams
    /// <summary>
    ///  Processing params of image to blob.
    /// </summary>
    /// <remarks>
    ///         It includes all possible image processing operations and corresponding parameters.
    ///        
    ///         @see blobFromImageWithParams
    ///        
    ///         @note
    ///         The order and usage of `scalefactor` and `mean` are (input - mean) * scalefactor.
    ///         The order and usage of `scalefactor`, `size`, `mean`, `swapRB`, and `ddepth` are consistent
    ///         with the function of @ref blobFromImage.
    /// </remarks>
    public partial class Image2BlobParams : DisposableOpenCVObject
    {

        protected override void Dispose(bool disposing)
        {

            try
            {
                if (disposing)
                {
                }
                if (IsEnabledDispose)
                {
                    if (nativeObj != IntPtr.Zero)
                        dnn_Image2BlobParams_delete(nativeObj);
                    nativeObj = IntPtr.Zero;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }

        }

        protected internal Image2BlobParams(IntPtr addr) : base(addr) { }


        public IntPtr getNativeObjAddr() { return nativeObj; }

        // internal usage only
        public static Image2BlobParams __fromPtr__(IntPtr addr) { return new Image2BlobParams(addr); }

        //
        // C++:   cv::dnn::Image2BlobParams::Image2BlobParams()
        //

        public Image2BlobParams()
        {


            nativeObj = DisposableObject.ThrowIfNullIntPtr(dnn_Image2BlobParams_Image2BlobParams_10());


        }


        //
        // C++:   cv::dnn::Image2BlobParams::Image2BlobParams(Scalar scalefactor, Size size = Size(), Scalar mean = Scalar(), bool swapRB = false, int ddepth = CV_32F, DataLayout datalayout = DNN_LAYOUT_NCHW, ImagePaddingMode mode = DNN_PMODE_NULL, Scalar borderValue = 0.0)
        //

        public Image2BlobParams(Scalar scalefactor, Size size, Scalar mean, bool swapRB, int ddepth, Scalar borderValue)
        {


            nativeObj = DisposableObject.ThrowIfNullIntPtr(dnn_Image2BlobParams_Image2BlobParams_11(scalefactor.val[0], scalefactor.val[1], scalefactor.val[2], scalefactor.val[3], size.width, size.height, mean.val[0], mean.val[1], mean.val[2], mean.val[3], swapRB, ddepth, borderValue.val[0], borderValue.val[1], borderValue.val[2], borderValue.val[3]));


        }

        public Image2BlobParams(Scalar scalefactor, Size size, Scalar mean, bool swapRB, int ddepth)
        {


            nativeObj = DisposableObject.ThrowIfNullIntPtr(dnn_Image2BlobParams_Image2BlobParams_12(scalefactor.val[0], scalefactor.val[1], scalefactor.val[2], scalefactor.val[3], size.width, size.height, mean.val[0], mean.val[1], mean.val[2], mean.val[3], swapRB, ddepth));


        }

        public Image2BlobParams(Scalar scalefactor, Size size, Scalar mean, bool swapRB)
        {


            nativeObj = DisposableObject.ThrowIfNullIntPtr(dnn_Image2BlobParams_Image2BlobParams_15(scalefactor.val[0], scalefactor.val[1], scalefactor.val[2], scalefactor.val[3], size.width, size.height, mean.val[0], mean.val[1], mean.val[2], mean.val[3], swapRB));


        }

        public Image2BlobParams(Scalar scalefactor, Size size, Scalar mean)
        {


            nativeObj = DisposableObject.ThrowIfNullIntPtr(dnn_Image2BlobParams_Image2BlobParams_16(scalefactor.val[0], scalefactor.val[1], scalefactor.val[2], scalefactor.val[3], size.width, size.height, mean.val[0], mean.val[1], mean.val[2], mean.val[3]));


        }

        public Image2BlobParams(Scalar scalefactor, Size size)
        {


            nativeObj = DisposableObject.ThrowIfNullIntPtr(dnn_Image2BlobParams_Image2BlobParams_17(scalefactor.val[0], scalefactor.val[1], scalefactor.val[2], scalefactor.val[3], size.width, size.height));


        }

        public Image2BlobParams(Scalar scalefactor)
        {


            nativeObj = DisposableObject.ThrowIfNullIntPtr(dnn_Image2BlobParams_Image2BlobParams_18(scalefactor.val[0], scalefactor.val[1], scalefactor.val[2], scalefactor.val[3]));


        }


        //
        // C++:  Rect cv::dnn::Image2BlobParams::blobRectToImageRect(Rect rBlob, Size size)
        //

        /// <summary>
        ///  Get rectangle coordinates in original image system from rectangle in blob coordinates.
        /// </summary>
        /// <param name="rBlob">
        /// rect in blob coordinates.
        /// </param>
        /// <param name="size">
        /// original input image size.
        /// </param>
        /// <returns>
        ///  rectangle in original image coordinates.
        /// </returns>
        public Rect blobRectToImageRect(Rect rBlob, Size size)
        {
            ThrowIfDisposed();

            double[] tmpArray = new double[4];
            dnn_Image2BlobParams_blobRectToImageRect_10(nativeObj, rBlob.x, rBlob.y, rBlob.width, rBlob.height, size.width, size.height, tmpArray);
            Rect retVal = new Rect(tmpArray);

            return retVal;
        }


        //
        // C++:  void cv::dnn::Image2BlobParams::blobRectsToImageRects(vector_Rect rBlob, vector_Rect& rImg, Size size)
        //

        /// <summary>
        ///  Get rectangle coordinates in original image system from rectangle in blob coordinates.
        /// </summary>
        /// <param name="rBlob">
        /// rect in blob coordinates.
        /// </param>
        /// <param name="rImg">
        /// result rect in image coordinates.
        /// </param>
        /// <param name="size">
        /// original input image size.
        /// </param>
        public void blobRectsToImageRects(MatOfRect rBlob, MatOfRect rImg, Size size)
        {
            ThrowIfDisposed();
            if (rBlob != null) rBlob.ThrowIfDisposed();
            if (rImg != null) rImg.ThrowIfDisposed();
            Mat rBlob_mat = rBlob;
            Mat rImg_mat = rImg;
            dnn_Image2BlobParams_blobRectsToImageRects_10(nativeObj, rBlob_mat.nativeObj, rImg_mat.nativeObj, size.width, size.height);


        }


        //
        // C++: Scalar Image2BlobParams::scalefactor
        //

        public Scalar get_scalefactor()
        {
            ThrowIfDisposed();

            double[] tmpArray = new double[4];
            dnn_Image2BlobParams_get_1scalefactor_10(nativeObj, tmpArray);
            Scalar retVal = new Scalar(tmpArray);

            return retVal;
        }


        //
        // C++: void Image2BlobParams::scalefactor
        //

        public void set_scalefactor(Scalar scalefactor)
        {
            ThrowIfDisposed();

            dnn_Image2BlobParams_set_1scalefactor_10(nativeObj, scalefactor.val[0], scalefactor.val[1], scalefactor.val[2], scalefactor.val[3]);


        }


        //
        // C++: Size Image2BlobParams::size
        //

        public Size get_size()
        {
            ThrowIfDisposed();

            double[] tmpArray = new double[2];
            dnn_Image2BlobParams_get_1size_10(nativeObj, tmpArray);
            Size retVal = new Size(tmpArray);

            return retVal;
        }


        //
        // C++: void Image2BlobParams::size
        //

        public void set_size(Size size)
        {
            ThrowIfDisposed();

            dnn_Image2BlobParams_set_1size_10(nativeObj, size.width, size.height);


        }


        //
        // C++: Scalar Image2BlobParams::mean
        //

        public Scalar get_mean()
        {
            ThrowIfDisposed();

            double[] tmpArray = new double[4];
            dnn_Image2BlobParams_get_1mean_10(nativeObj, tmpArray);
            Scalar retVal = new Scalar(tmpArray);

            return retVal;
        }


        //
        // C++: void Image2BlobParams::mean
        //

        public void set_mean(Scalar mean)
        {
            ThrowIfDisposed();

            dnn_Image2BlobParams_set_1mean_10(nativeObj, mean.val[0], mean.val[1], mean.val[2], mean.val[3]);


        }


        //
        // C++: bool Image2BlobParams::swapRB
        //

        public bool get_swapRB()
        {
            ThrowIfDisposed();

            return dnn_Image2BlobParams_get_1swapRB_10(nativeObj);


        }


        //
        // C++: void Image2BlobParams::swapRB
        //

        public void set_swapRB(bool swapRB)
        {
            ThrowIfDisposed();

            dnn_Image2BlobParams_set_1swapRB_10(nativeObj, swapRB);


        }


        //
        // C++: int Image2BlobParams::ddepth
        //

        public int get_ddepth()
        {
            ThrowIfDisposed();

            return dnn_Image2BlobParams_get_1ddepth_10(nativeObj);


        }


        //
        // C++: void Image2BlobParams::ddepth
        //

        public void set_ddepth(int ddepth)
        {
            ThrowIfDisposed();

            dnn_Image2BlobParams_set_1ddepth_10(nativeObj, ddepth);


        }


        //
        // C++: DataLayout Image2BlobParams::datalayout
        //

        // Return type 'DataLayout' is not supported, skipping the function


        //
        // C++: void Image2BlobParams::datalayout
        //

        // Unknown type 'DataLayout' (I), skipping the function


        //
        // C++: ImagePaddingMode Image2BlobParams::paddingmode
        //

        // Return type 'ImagePaddingMode' is not supported, skipping the function


        //
        // C++: void Image2BlobParams::paddingmode
        //

        // Unknown type 'ImagePaddingMode' (I), skipping the function


        //
        // C++: Scalar Image2BlobParams::borderValue
        //

        public Scalar get_borderValue()
        {
            ThrowIfDisposed();

            double[] tmpArray = new double[4];
            dnn_Image2BlobParams_get_1borderValue_10(nativeObj, tmpArray);
            Scalar retVal = new Scalar(tmpArray);

            return retVal;
        }


        //
        // C++: void Image2BlobParams::borderValue
        //

        public void set_borderValue(Scalar borderValue)
        {
            ThrowIfDisposed();

            dnn_Image2BlobParams_set_1borderValue_10(nativeObj, borderValue.val[0], borderValue.val[1], borderValue.val[2], borderValue.val[3]);


        }


#if (UNITY_IOS || UNITY_WEBGL) && !UNITY_EDITOR
        const string LIBNAME = "__Internal";
#else
        const string LIBNAME = "opencvforunity";
#endif



        // C++:   cv::dnn::Image2BlobParams::Image2BlobParams()
        [DllImport(LIBNAME)]
        private static extern IntPtr dnn_Image2BlobParams_Image2BlobParams_10();

        // C++:   cv::dnn::Image2BlobParams::Image2BlobParams(Scalar scalefactor, Size size = Size(), Scalar mean = Scalar(), bool swapRB = false, int ddepth = CV_32F, DataLayout datalayout = DNN_LAYOUT_NCHW, ImagePaddingMode mode = DNN_PMODE_NULL, Scalar borderValue = 0.0)
        [DllImport(LIBNAME)]
        private static extern IntPtr dnn_Image2BlobParams_Image2BlobParams_11(double scalefactor_val0, double scalefactor_val1, double scalefactor_val2, double scalefactor_val3, double size_width, double size_height, double mean_val0, double mean_val1, double mean_val2, double mean_val3, [MarshalAs(UnmanagedType.U1)] bool swapRB, int ddepth, double borderValue_val0, double borderValue_val1, double borderValue_val2, double borderValue_val3);
        [DllImport(LIBNAME)]
        private static extern IntPtr dnn_Image2BlobParams_Image2BlobParams_12(double scalefactor_val0, double scalefactor_val1, double scalefactor_val2, double scalefactor_val3, double size_width, double size_height, double mean_val0, double mean_val1, double mean_val2, double mean_val3, [MarshalAs(UnmanagedType.U1)] bool swapRB, int ddepth);
        [DllImport(LIBNAME)]
        private static extern IntPtr dnn_Image2BlobParams_Image2BlobParams_15(double scalefactor_val0, double scalefactor_val1, double scalefactor_val2, double scalefactor_val3, double size_width, double size_height, double mean_val0, double mean_val1, double mean_val2, double mean_val3, [MarshalAs(UnmanagedType.U1)] bool swapRB);
        [DllImport(LIBNAME)]
        private static extern IntPtr dnn_Image2BlobParams_Image2BlobParams_16(double scalefactor_val0, double scalefactor_val1, double scalefactor_val2, double scalefactor_val3, double size_width, double size_height, double mean_val0, double mean_val1, double mean_val2, double mean_val3);
        [DllImport(LIBNAME)]
        private static extern IntPtr dnn_Image2BlobParams_Image2BlobParams_17(double scalefactor_val0, double scalefactor_val1, double scalefactor_val2, double scalefactor_val3, double size_width, double size_height);
        [DllImport(LIBNAME)]
        private static extern IntPtr dnn_Image2BlobParams_Image2BlobParams_18(double scalefactor_val0, double scalefactor_val1, double scalefactor_val2, double scalefactor_val3);

        // C++:  Rect cv::dnn::Image2BlobParams::blobRectToImageRect(Rect rBlob, Size size)
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_blobRectToImageRect_10(IntPtr nativeObj, int rBlob_x, int rBlob_y, int rBlob_width, int rBlob_height, double size_width, double size_height, double[] retVal);

        // C++:  void cv::dnn::Image2BlobParams::blobRectsToImageRects(vector_Rect rBlob, vector_Rect& rImg, Size size)
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_blobRectsToImageRects_10(IntPtr nativeObj, IntPtr rBlob_mat_nativeObj, IntPtr rImg_mat_nativeObj, double size_width, double size_height);

        // C++: Scalar Image2BlobParams::scalefactor
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_get_1scalefactor_10(IntPtr nativeObj, double[] retVal);

        // C++: void Image2BlobParams::scalefactor
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_set_1scalefactor_10(IntPtr nativeObj, double scalefactor_val0, double scalefactor_val1, double scalefactor_val2, double scalefactor_val3);

        // C++: Size Image2BlobParams::size
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_get_1size_10(IntPtr nativeObj, double[] retVal);

        // C++: void Image2BlobParams::size
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_set_1size_10(IntPtr nativeObj, double size_width, double size_height);

        // C++: Scalar Image2BlobParams::mean
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_get_1mean_10(IntPtr nativeObj, double[] retVal);

        // C++: void Image2BlobParams::mean
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_set_1mean_10(IntPtr nativeObj, double mean_val0, double mean_val1, double mean_val2, double mean_val3);

        // C++: bool Image2BlobParams::swapRB
        [DllImport(LIBNAME)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool dnn_Image2BlobParams_get_1swapRB_10(IntPtr nativeObj);

        // C++: void Image2BlobParams::swapRB
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_set_1swapRB_10(IntPtr nativeObj, [MarshalAs(UnmanagedType.U1)] bool swapRB);

        // C++: int Image2BlobParams::ddepth
        [DllImport(LIBNAME)]
        private static extern int dnn_Image2BlobParams_get_1ddepth_10(IntPtr nativeObj);

        // C++: void Image2BlobParams::ddepth
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_set_1ddepth_10(IntPtr nativeObj, int ddepth);

        // C++: Scalar Image2BlobParams::borderValue
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_get_1borderValue_10(IntPtr nativeObj, double[] retVal);

        // C++: void Image2BlobParams::borderValue
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_set_1borderValue_10(IntPtr nativeObj, double borderValue_val0, double borderValue_val1, double borderValue_val2, double borderValue_val3);

        // native support for java finalize()
        [DllImport(LIBNAME)]
        private static extern void dnn_Image2BlobParams_delete(IntPtr nativeObj);

    }
}

#endif