using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Basler.Pylon;

namespace CameraHardwareTriggering
{
    public partial class Form1 : Form
    {
        Camera camera = null;

        double WidthRatio;
        double HeightRatio;
        double difference_x;
        double difference_y;

        long frameWidth = 0;
        long frameHeight = 0;
        double FPS = 0;
        const int countOfImagesToGrab = 6000;
        int videoNum = 0;
        string VideoName;

        CancellationTokenSource tokenSource = new CancellationTokenSource();

        DialogResult result;

        private PixelDataConverter converter = new PixelDataConverter();

        private enum recordingInstruction
        {
            start,
            stop,
        }

        public Form1()
        {
            InitializeComponent();
            List<ICameraInfo> allCameraInfo = CameraFinder.Enumerate();
            timerRecipe.Start();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void OnConnectionLost(Object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnConnectionLost), sender, e);
                return;
            }

            // Close the camera object.
            DestroyCamera();

            // Because one device is gone, the list needs to be updated.
            timerRecipe.Start();
        }

        private void OnCameraOpened(Object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnCameraOpened), sender, e);
                return;
            }
        }

        private void OnCameraClosed(Object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnCameraClosed), sender, e);
                return;
            }
        }

        private void OnGrabStarted(Object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<EventArgs>(OnGrabStarted), sender, e);
                return;
            }

            timerRecipe.Stop();
        }

        private void OnGrabStopped(Object sender, GrabStopEventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper thread.
                BeginInvoke(new EventHandler<GrabStopEventArgs>(OnGrabStopped), sender, e);
                return;
            }
        }

        private void OnImageGrabbed(Object sender, ImageGrabbedEventArgs e)
        {
            if (InvokeRequired)
            {
                // If called from a different thread, we must use the Invoke method to marshal the call to the proper GUI thread.
                // The grab result will be disposed after the event call. Clone the event arguments for marshaling to the GUI thread.
                BeginInvoke(new EventHandler<ImageGrabbedEventArgs>(OnImageGrabbed), sender, e.Clone());
                return;
            }

            try
            {
                // Get the grab result.
                IGrabResult grabResult = e.GrabResult;

                /* Check if the image has been removed in the meantime. */
                if (grabResult.IsValid)
                {
                    Bitmap bitmap = new Bitmap(grabResult.Width, grabResult.Height, PixelFormat.Format32bppRgb);
                    // Lock the bits of the bitmap.
                    BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                    // Place the pointer to the buffer of the bitmap.
                    converter.OutputPixelFormat = PixelType.BGRA8packed;
                    IntPtr ptrBmp = bmpData.Scan0;
                    converter.Convert(ptrBmp, bmpData.Stride * bitmap.Height, grabResult); //Exception handling TODO
                    bitmap.UnlockBits(bmpData);

                    // Assign a temporary variable to dispose the bitmap after assigning the new bitmap to the display control.
                    Bitmap bitmapOld = pictureBox1.Image as Bitmap;

                    // Provide the display control with the new bitmap. This action automatically updates the display.
                    pictureBox1.Image = bitmap;
                    pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
                    pictureBox1.Refresh();

                    // save image && calculate ratio
                    //save_image(bitmap);
                    calculateWidthHeightRatio(pictureBox1, bitmap);

                    if (bitmapOld != null)
                    {
                        // Dispose the bitmap.
                        bitmapOld.Dispose();
                    }
                }
                // Live inspection 
                //live_inspection();
            }
            catch (Exception exception)
            {
                ShowException(exception);
            }
            finally
            {
                // Dispose the grab result if needed for returning it to the grab loop.
                e.DisposeGrabResultIfClone();
            }
        }

        private void ShowException(Exception e, string additionalErrorMessage)
        {
            string more = "\n\nLast error message (may not belong to the exception):\n" + additionalErrorMessage;
            MessageBox.Show("Exception caught:\n" + e.Message + (additionalErrorMessage.Length > 0 ? more : ""), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // Shows exceptions in a message box.
        private void ShowException(Exception exception)
        {
            result = MessageBox.Show("Exception caught:\n" + exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void UpdateDeviceList()
        {
            try
            {
                /* Ask the device enumerator for a list of devices. */
                List<ICameraInfo> allCameras = CameraFinder.Enumerate();
                //List<DeviceEnumerator.Device> list = DeviceEnumerator.EnumerateDevices();
                //ListView.ListViewItemCollection items = listView1.Items;

                /* Add each new device to the list. */
                foreach (ICameraInfo cameraInfo in allCameras)
                {

                    try
                    {
                        camera = new Camera(cameraInfo);
                        camera.CameraOpened += Configuration.AcquireContinuous;

                        
                        
                       

                        // Register for the events of the image provider needed for proper operation.
                        camera.ConnectionLost += OnConnectionLost;
                        camera.CameraOpened += OnCameraOpened;
                        camera.CameraClosed += OnCameraClosed;
                        camera.StreamGrabber.GrabStarted += OnGrabStarted;
                        camera.StreamGrabber.ImageGrabbed += OnImageGrabbed;
                        camera.StreamGrabber.GrabStopped += OnGrabStopped;




                        // Open the connection to the camera device.
                        camera.Open();
                        CameraTB.Text = "Connected";
                        CameraTB.Refresh();

                        timerRecipe.Stop();

                        camera.Parameters[PLCamera.TriggerMode].SetValue(PLCamera.TriggerMode.On);
                        camera.Parameters[PLCamera.TriggerSource].SetValue(PLCamera.TriggerSource.Line1);
                        camera.Parameters[PLCamera.TriggerActivation].SetValue(PLCamera.TriggerActivation.RisingEdge);

                        //get the camera setting
                        frameWidth = camera.Parameters[PLCamera.Width].GetValue();
                        frameHeight = camera.Parameters[PLCamera.Height].GetValue();
                        FPS = Math.Floor(camera.Parameters[PLCamera.ResultingFrameRate].GetValue());
                    }
                    catch (Exception ex)
                    {
                        timerRecipe.Stop();
                        ShowException(ex);

                        if (result == DialogResult.OK)
                        {
                            timerRecipe.Start();
                        }

                    }
                }


            }
            catch (Exception exception)
            {
                ShowException(exception);
            }
        }

        private void DestroyCamera()
        {
            // Destroy the camera object.
            try
            {
                if (camera != null)
                {
                    camera.Close();
                    camera.Dispose();
                    camera = null;
                }
            }
            catch (Exception exception)
            {
                ShowException(exception);
            }
        }

        public void Stop()
        {
            // Stop the grabbing.
            try
            {
                camera.StreamGrabber.Stop();
            }
            catch (Exception exception)
            {
                ShowException(exception);
            }

        }
        private void timerRecipe_Tick(object sender, EventArgs e)
        {
            UpdateDeviceList();
        }

        /* Starts the grabbing of one image and handles exceptions. */
        private void OneShot()
        {
            try
            {
                // Starts the grabbing of one image.
                camera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.SingleFrame);
                camera.StreamGrabber.Start(1, GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            }
            catch (Exception exception)
            {
                ShowException(exception);
            }
        }

        private void ContinuousShot()
        {
            try
            {
                // Start the grabbing of images until grabbing is stopped.
                camera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                camera.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            }
            catch (Exception exception)
            {
                ShowException(exception);

            }
        }


        private void calculateWidthHeightRatio(PictureBox PB, Image img)
        {
            double ori_WidthHeightRatio = (double)img.Height / (double)img.Width;

            if (pictureBox1.Width * ori_WidthHeightRatio > pictureBox1.Height)
            {
                HeightRatio = (double)PB.Height / (double)img.Height;
                WidthRatio = HeightRatio;
                difference_x = ((double)PB.Width - ((PB.Height * img.Width) / img.Height)) / 2;
                difference_y = 0;
            }
            else
            {
                WidthRatio = (double)PB.Width / (double)img.Width;
                HeightRatio = WidthRatio;
                difference_y = ((double)PB.Height - ((PB.Width * img.Height) / img.Width)) / 2;
                difference_x = 0;
            }

        }

        private void startButton_Click(object sender, EventArgs e)
        {
            videoGrabbingAction(recordingInstruction.start);
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            videoGrabbingAction(recordingInstruction.stop);
        }

        private void videoGrabbingAction(recordingInstruction action)
        {
            videoNum += 1;
            if (videoNum > 1)
            {
                tokenSource = new CancellationTokenSource();
            }

            if (action == recordingInstruction.start)
            {

                var token = tokenSource.Token;

                // capture video
                Task t;
                t = Task.Run(() => Grab_video(token), token);

            }
            else
            {
                tokenSource.Cancel();
            }
        }

        private void timerRecipe_Tick_1(object sender, EventArgs e)
        {
            UpdateDeviceList();
        }

        public void Grab_video(CancellationToken ct)
        {

            try
            {
                if (ct.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();
                }

                camera.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Stop recording");
            }
        }
    }
}
