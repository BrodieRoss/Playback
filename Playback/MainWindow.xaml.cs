using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Playback.NaudioCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Playback
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        private DispatcherTimer timer;
        private WasapiOut wasapiOut;
       // private SynchronizationContext synchronizationContext;
        private MMDevice selectedRecordingDevice; //microphone device 
        private MMDevice selectedPlaybackDevice;
        private WasapiCapture microphone;
        private AudioFileReader reader;
        private string filePath;
        private bool drc;
        public MainWindow()
        {
            InitializeComponent();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += OnTimerTick;

            PopulateDevicesList();
        }
        //class declarations for reader and output objects


        //Wasapi implemention for fetching devices
        private void PopulateDevicesList()
        {
            var devicesEnumerator = new MMDeviceEnumerator();  //object which allows access to audio endpoints
            var devices = devicesEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            var defaultDevice = devicesEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);//fetch default device in wasapi
            foreach (var device in devices)
            {
                ComboDevices.Items.Add(device);

            }

            ComboDevices.Text = defaultDevice.FriendlyName;
        }

        void OnTimerTick(object sender, EventArgs e)
        {
            if (reader != null)
            {
                textBlockPosition.Text = reader.CurrentTime.ToString();
                sliderPosition.Value = reader.CurrentTime.TotalSeconds;
            }
        }



        private void OnPlayClick(object sender, RoutedEventArgs e)
        {

            InitialisePlayback();
            InitialiseRecording();
            EnableControls(true);// enable stop disable play
        }



        private void InitialisePlayback()
        {
            selectedPlaybackDevice = (MMDevice)ComboDevices.SelectedItem;
            AudioClientShareMode shareMode = AudioClientShareMode.Shared;//set sound card to be shared between applications this is usually the default setting
            int latency = 20; //wasapi can work at much lower latencie
            bool useEventSync = false;
            wasapiOut = new WasapiOut(selectedPlaybackDevice, shareMode, useEventSync, latency);
            /*device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)sliderVolume.Value;*///set volume on physical device as opposed to the wasapi session , apparently this may be integrated into Naudio later
            drc = ApplyDRC.IsChecked.Value;// ascertain whether to apply drc
            wasapiOut.PlaybackStopped += OnPlaybackStopped;
            //  reader = new AudioFileReader("Paranoid.mp3");
            reader = new AudioFileReader(filePath);

            textBlockDuration.Text = reader.TotalTime.ToString(); //set slider display values
            textBlockPosition.Text = reader.CurrentTime.ToString();
            sliderPosition.Maximum = reader.TotalTime.TotalSeconds;//set max value of slider to track length

            if (drc)
            {
                CompressionReader drcaudio = new CompressionReader(filePath);
                wasapiOut.Init(drcaudio);
            }

            else
            {
                timer.Start();
                reader.Volume = (float)sliderVolume.Value;
                wasapiOut.Init(reader);
            }

            wasapiOut.Play();
        }

        private void InitialiseRecording()
        {
            var enumerator = new MMDeviceEnumerator();
            selectedRecordingDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture,Role.Communications);
            microphone = new WasapiCapture(selectedRecordingDevice);
            //microphone.ShareMode
            selectedRecordingDevice.AudioEndpointVolume.MasterVolumeLevelScalar = 1;
            microphone.StartRecording();
            microphone.RecordingStopped += OnRecordingStopped;
            microphone.DataAvailable += CaptureOnDataAvailable;


        }
    

        /// <summary>
        /// Still very heavily in progress implements sidechain amplification by increasing endpoint volume when mean of samples becomes 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="waveInEventArgs"></param>
        private void CaptureOnDataAvailable(object sender, WaveInEventArgs waveInEventArgs)
        {
            byte[] buffer = waveInEventArgs.Buffer;
            int bytesRecorded = waveInEventArgs.BytesRecorded;
            float sampleTotal = 0f;
            float sampleCount = 0;
            //code modified from Mark Heath's peak meter example
            for (int index=0;index<waveInEventArgs.BytesRecorded;index+=2)
            {
                short sample = (short)((buffer[index + 1] << 8) | //each sample is composed of two bytes in the byte array
                                      buffer[index + 0]);
                float sample32 = (sample / 32768f)*2; // not sure why but samples only register as half of their actual volume when measured against other software so they have been doubled
                sampleTotal += Math.Abs(sample32); //retrieve a positive value for the sample
                sampleCount++;
            }

            float sampleMean =(sampleTotal / sampleCount);
         
            this.Dispatcher.Invoke(() => { // code for passing values from wasapi background thread to GUI thread 
                meanMeter.Value=sampleMean; //set meter which displays mean values from buffer
                if (sampleMean > 0.9 && ApplyAVA.IsChecked==true)
                {
                    selectedPlaybackDevice.AudioEndpointVolume.MasterVolumeLevelScalar = sampleMean; //set volume on physical device according to microphone input just testing at the minute will change to threshold ratio system later
                }
            });
          
        }

        //void UpdatePeakMeter()
        //{
        //    synchronizationContext.Post(s => Peak = selectedDevice.AudioMeterInformation
        //       .MasterPeakValue, null);


        //}

        //public float Peak
        //{
        //    get { return peak; }
        //    set
        //    {
        //        // ReSharper disable once CompareOfFloatsByEqualityOperator
        //        if (peak != value)
        //        {
        //            peak = value;
        //            OnPropertyChanged("Peak");
        //        }
        //    }
        //}

        //public MMDevice SelectedDevice
        //{
        //    get { return selectedDevice; }
        //    set
        //    {
        //        if (selectedDevice != value)
        //        {
        //            selectedDevice = value;
        //            OnPropertyChanged("SelectedDevice");
        //            GetDefaultRecordingFormat(value);
        //        }
        //    }
        //}

        /// <summary>
        /// dispose of capture related objects
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnRecordingStopped(object sender, StoppedEventArgs e) 
        {
        
            if (e.Exception!=null)
            {
                if (e.Exception.Message == null)
                {
                    MessageBox.Show("Error, recording stopped");
                }
                else
                {
                    MessageBox.Show("Error:" + e.Exception.Message);
                }
            }
            microphone.Dispose();
            microphone = null;
            meanMeter.Value = 0;

        }
        void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            timer.Stop();
            //sets slider back to starting value
            sliderPosition.Value = 0;
            reader.Dispose();
            wasapiOut.Dispose();
            EnableControls(false);
            if (e.Exception != null)
            {
                MessageBox.Show(e.Exception.Message);
            }
        }

        void HandleChecked(object sender, RoutedEventArgs e)
        {
            drc = true;
        }

        void HandleUnChecked(object sender, RoutedEventArgs e)
        {
            drc = false;
        }


        /// <summary>
        /// Completion event triggered by user letting go of slider, sets song position to appropriate timespan
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SliderPositionOnDragCompleted(object sender,  DragCompletedEventArgs e)
        {
            if (reader!=null)
            {
                reader.CurrentTime = TimeSpan.FromSeconds(sliderPosition.Value);
            }
        }

        //stop the output when the stop button is hit
        private void OnStopClicked(object sender,RoutedEventArgs e)
        {
            wasapiOut.Stop();
            microphone.StopRecording();
        }

        private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
        

            if(wasapiOut!=null)
            {
                //var device = (MMDevice)ComboDevices.SelectedItem;
                //device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)sliderVolume.Value;
                reader.Volume = (float)sliderVolume.Value; //volume implementation with file reader instead of hardware endpoint
            }

        }



        /// <summary>
        /// Enables slider and stop button once play button is hit
        /// </summary>
        /// <param name="isPlaying"></param>
        private void EnableControls(bool isPlaying)
        {
          
                buttonPlay.IsEnabled = !isPlaying;
              
        BrowseFile.IsEnabled = !isPlaying;
          
            buttonStop.IsEnabled = isPlaying;
            sliderPosition.IsEnabled = isPlaying;
        
        }


        /// <summary>
        /// open file dialog and set filename once selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBrowseClick(object sender,RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                FileName.Text = openFileDialog.SafeFileName;
                filePath = openFileDialog.FileName;
                EnableControls(false);//enable play button after file has been selectec
            }
            
        }
    }
}
