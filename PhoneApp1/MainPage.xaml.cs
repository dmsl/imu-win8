/* Copyright (C) 2013 Philippos Papaphilippou, Philokypros Ioulianou
 *
 * @version    : 1.0
 * @author     : Philippos Papaphilippou (philippos.info) ppapap01[at]cs.ucy.ac.cy
 * @author     : Philokypros Ioulianou                    fiouli01[at]cs.ucy.ac.cy
 *
 * Data Management Systems Laboratory (DMSL)
 * Department of Computer Science
 * University of Cyprus
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * at your option) any later version.
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 * Υou should have received a copy of the GNU General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * **PEDOMETER CODE was found freely available at a public domain at 
 * http://stackoverflow.com/questions/9895402/wp7-sdk-pedometer
 * 
 * ***The overall code about sensor usage is based heavily on WP8 dev samples.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using Microsoft.Devices.Sensors;
using Microsoft.Phone.Shell;
using System.Windows.Navigation;
using PhoneApp1.Resources;

// For Maps
using System.Device.Location;
using Microsoft.Phone.Maps.Controls;
using Microsoft.Phone.Maps.Toolkit;

// For Compass
using Microsoft.Xna.Framework;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Windows.Devices.Geolocation;
using System.Threading.Tasks;

namespace PhoneApp1
{
    public partial class MainPage : PhoneApplicationPage
    {
        GeoCoordinateWatcher watcher;
        Accelerometer accelerometer;
        Vector3 acceleration;

        #region Initialization // - Philippos

        // For image in maps 
        double prevWidth;
        double initialWidth = 200; 
        double prevZoom;
        double initialZoom = 18;
 
        // For compass measurements     
        Compass compass;
        DispatcherTimer timer;
        double trueHeading;
        double headingAccuracy;
        bool calibrating = false;

        // Pedometer (For measuring steps)
        int steplength = 50;
        bool hasChanged;
        int stepcount = 0; 
        double x_old;
        double y_old;
        double z_old;
        float sensitivity = 0.9f;

        double R = 6378.137; // Radius of earth in Km
        double curLat = 35.144792;
        double curLon = 33.411099;
             
        // Constructor
        public MainPage()
        {
            InitializeComponent();          
           
            txtlat.Text = curLat.ToString();
            txtlon.Text = curLon.ToString();
            statusbox.Text = "(tap once on the map to set a start point)";

            Application.Current.Host.Settings.EnableFrameRateCounter = false;

            if (!Compass.IsSupported)
            {
                // The device doesn't have compass sensor. 
                statusbox.Text = "Compass is not available!";
                ApplicationBar.IsVisible = false;
            }
            else
            {
                // Initialize the timer and add Tick event handler.
                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(30);
                timer.Tick += new EventHandler(timer_Tick);
            }
            this.Loaded += MainPage_Loaded;
        }

        // Step select screen - Philokypros
        private void stepButton_Click(object sender, RoutedEventArgs e)
        {
            stepStackPanel.Visibility = Visibility.Collapsed;
            txtcaption.Visibility = Visibility.Visible;
            txtst.Visibility = Visibility.Visible;
            txtstep.Visibility = Visibility.Visible;
            txtlat.Visibility = Visibility.Visible;
            txtlon.Visibility = Visibility.Visible;
            latbox.Visibility = Visibility.Visible;
            lonbox.Visibility = Visibility.Visible;
            if (rd1.IsChecked == true)
            {
                steplength = 50;
            }
            if (rd2.IsChecked == true)
            {
                steplength = 60;
            }
            if (rd3.IsChecked == true)
            {
                steplength = 70;
            }
            if (rd4.IsChecked == true)
            {
                steplength = 80;
            }
            if (rd5.IsChecked == true)
            {
                steplength = 90;
            }
           
            int n=0;
            bool isNumeric=true;
            if (rd6.IsChecked == true && txtcustomstep.Text != " ")
                isNumeric= int.TryParse(txtcustomstep.Text, out n);
            if (rd6.IsChecked == true && txtcustomstep.Text!=" " &&( !isNumeric || n<=0))
            {
                MessageBox.Show("Please enter a valid number!", "Error!", MessageBoxButton.OK);
                stepStackPanel.Visibility = Visibility.Visible;
                txtcaption.Visibility = Visibility.Collapsed;
                txtst.Visibility = Visibility.Collapsed;
                txtstep.Visibility = Visibility.Collapsed;
            }
            
            if (rd6.IsChecked == true && txtcustomstep.Text != " ")
            {
                rd6.IsChecked = true;
                steplength = n;
            } 
            txtstep.Text = steplength.ToString() +" cm";
        }
       
        void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Insert Map - Philokypros
            map.Tap += map_Tap;
            MapLayer myLayer = new MapLayer();
            MapOverlay myOverlay = new MapOverlay()
            {
                GeoCoordinate = new GeoCoordinate(35.144925, 33.410800)
            };
            ExpanderView expander = new ExpanderView();

            expander.Header = new Image()
            {
                Source = new BitmapImage(new Uri("/images/2.png", UriKind.Relative)),
                Width = prevWidth = initialWidth
            };
                        
            map.CartographicMode = MapCartographicMode.Hybrid;
            map.LandmarksEnabled = true; 

            prevZoom = map.ZoomLevel = initialZoom;

            // Draw start point - Philippos
            Polygon polygon = new Polygon();
            polygon.Points.Add(new System.Windows.Point(0, 0));
            polygon.Points.Add(new System.Windows.Point(0, 25));
            polygon.Points.Add(new System.Windows.Point(25, 0));
            polygon.Fill = new SolidColorBrush(Colors.Blue);
            polygon.Tag = new GeoCoordinate(curLat, curLon);
            MapOverlay overlay = new MapOverlay();
            overlay.Content = polygon;
            overlay.GeoCoordinate = new GeoCoordinate (curLat, curLon);
            overlay.PositionOrigin = new System.Windows.Point(0.0, 1.0);

            myLayer.Add(overlay);
               
            myOverlay.Content = expander;
            myLayer.Add(myOverlay);           
            map.Layers.Add(myLayer);                    
        }

        // On map tap set starting point - Philippos, Philokypros
        void  map_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            System.Windows.Point p = e.GetPosition(map);
            GeoCoordinate s = map.ConvertViewportPointToGeoCoordinate(p);            
            curLat = s.Latitude;
            curLon = s.Longitude;

            // Redraw map and image - Philippos
            map.Layers.Clear();
            MapLayer myLayer = new MapLayer();
            MapOverlay myOverlay = new MapOverlay()
            {
                GeoCoordinate = new GeoCoordinate(35.144925, 33.410800)
            };

            ExpanderView expander = new ExpanderView();
            expander.Header = new Image()
            {
                Source = new BitmapImage(new Uri("/images/2.png", UriKind.Relative)),
                Width = prevWidth
            };

            myOverlay.Content = expander;
            myLayer.Add(myOverlay);

            // Draw start point - Philippos
            Polygon polygon = new Polygon();
            polygon.Points.Add(new System.Windows.Point(0, 0));
            polygon.Points.Add(new System.Windows.Point(0, 25));
            polygon.Points.Add(new System.Windows.Point(25, 0));
            polygon.Fill = new SolidColorBrush(Colors.Blue);
            polygon.Tag = new GeoCoordinate(curLat, curLon);
            MapOverlay overlay = new MapOverlay();
            overlay.Content = polygon;
            overlay.GeoCoordinate = s;
            overlay.PositionOrigin = new System.Windows.Point(0.0, 1.0);            

            myLayer.Add(overlay);
            map.Layers.Add(myLayer);                  
               
        }
       
        // Quit screen - Philokypros
        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            string caption = "Do you really want to exit?";
            string message = "Press OK to terminate the application.";
            e.Cancel = MessageBoxResult.Cancel == MessageBox.Show(message,caption, MessageBoxButton.OKCancel);
            base.OnBackKeyPress(e);
        }

        #endregion
        #region User Interface

        private void step_bt(object sender, EventArgs e)
        {
            txtcaption.Visibility = Visibility.Collapsed;
            txtst.Visibility = Visibility.Collapsed; 
            txtstep.Visibility = Visibility.Collapsed;
            txtlat.Visibility = Visibility.Collapsed;
            txtlon.Visibility = Visibility.Collapsed;
            latbox.Visibility = Visibility.Collapsed;
            lonbox.Visibility = Visibility.Collapsed;
            statebox.Visibility = Visibility.Collapsed;
            statusbox.Visibility = Visibility.Collapsed; 
            Dispatcher.BeginInvoke(() => { stepStackPanel.Visibility = Visibility.Visible; });           
        }
        
        private void Zoom_in(object sender, EventArgs e)
        {
            map.ZoomLevel = Math.Min(map.ZoomLevel + 1, 20);
        }

        private void Zoom_out(object sender, EventArgs e)
        {
            map.ZoomLevel = Math.Max(map.ZoomLevel - 1, 1);
        }               
       
        #endregion

        #region Event Handling
       
        // COMPASS
        void compass_CurrentValueChanged(object sender, SensorReadingEventArgs<CompassReading> e)
        {            
            trueHeading = e.SensorReading.TrueHeading;
            headingAccuracy = Math.Abs(e.SensorReading.HeadingAccuracy);  
        }

        void timer_Tick(object sender, EventArgs e)
        {
            if (accelerometer.IsDataValid) { 
                //Accellerometer Readings - Philippos
                x.Text = acceleration.X.ToString("0.00");
                y.Text = acceleration.Y.ToString("0.00");
                z.Text = acceleration.Z.ToString("0.00");
                steps.Text = stepcount.ToString();

                // Draw Step path - Philippos
                if (compass.IsDataValid && hasChanged)
                {
                    MapPolyline line = new MapPolyline();
                    line.StrokeColor = Colors.Red;
                    line.StrokeThickness = 10;
                    line.Path.Add(new GeoCoordinate(curLat, curLon));

                    // Calculate Step using Cosine Rule - Philippos
                    double step = Math.Acos( 1-Math.Pow(steplength, 2)/Math.Pow(1000 * 100, 2)  /  (2 * Math.Pow(R, 2)) );
                    step = step *180 / Math.PI; 
                    curLat += step * Math.Cos((trueHeading) * Math.PI/180);
                    curLon += step * Math.Sin((trueHeading) * Math.PI/180);
                    txtlat.Text = curLat.ToString();
                    txtlon.Text = curLon.ToString();
                    line.Path.Add(new GeoCoordinate(curLat, curLon));
                    map.MapElements.Add(line);
                    hasChanged = false;
                }

            }

            if (!calibrating)
            {
                if (compass.IsDataValid)
                {
                    statusbox.Text = "Capturing steps...";
                }    
                                
                truel.Text = trueHeading.ToString("0.0");

            }
            else
            {
                if (headingAccuracy <= 10)
                {
                    calibrationTextBlock.Foreground = new SolidColorBrush(Colors.Green);
                    calibrationTextBlock.Text = "Complete!";
                }
                else
                {
                    calibrationTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                    calibrationTextBlock.Text = headingAccuracy.ToString("0.0");
                }
            }
        }

        void compass_Calibrate(object sender, CalibrationEventArgs e)
        {
            Dispatcher.BeginInvoke(() => { calibrationStackPanel.Visibility = Visibility.Visible; });
            calibrating = true;
        }

        private void calibrationButton_Click(object sender, RoutedEventArgs e)
        {
            calibrationStackPanel.Visibility = Visibility.Collapsed;
            calibrating = false;
        }

        void accelerometer_CurrentValueChanged(object sender, SensorReadingEventArgs<AccelerometerReading> e)
        {
       
            // New accellerometer            
            acceleration = e.SensorReading.Acceleration;

            if (compass.IsDataValid && !calibrating) 
            {

                // PEDOMETER CODE (available at http://stackoverflow.com/questions/9895402/wp7-sdk-pedometer)                
                float x = acceleration.X;
                float y = acceleration.Y;
                float z = acceleration.Z;
                double oldValue = ((x_old * x) + (y_old * y)) + (z_old * z);
                double oldValueSqrt = Math.Abs(Math.Sqrt((double)(((x_old * x_old) + (y_old * y_old)) + (z_old * z_old))));
                double newValue = Math.Abs(Math.Sqrt((double)(((x * x) + (y * y)) + (z * z))));
                oldValue /= oldValueSqrt * newValue;
                if ((oldValue <= sensitivity + 0.078) && (oldValue > sensitivity))  // Form here we could control sensitivity
                {
                    if (!hasChanged)
                    {
                        hasChanged = true;
                        stepcount++; // here we count steps                      
                    }
                    else
                    {
                        hasChanged = false;
                    }
                }
                x_old = x;
                y_old = y;
                z_old = z;
            }
            map.HeadingChanged += map_HeadingChanged;
        }

        void map_HeadingChanged(object sender, MapHeadingChangedEventArgs e)
        {
            map.SetView(new GeoCoordinate(curLat,curLon), map.ZoomLevel, trueHeading, MapAnimationKind.Parabolic);
        }

        #endregion

        private void onoff_Click(object sender, EventArgs e)
        {

            // ACCELLEROMETER ON/OFF
            if (accelerometer != null && accelerometer.IsDataValid)
            {
                // Stop data acquisition from the accelerometer.
                accelerometer.Stop();
                timer.Stop();
                stepcount = 0;
            }
            else
            {
                if (accelerometer == null)
                {
                    // Instantiate the accelerometer.
                    accelerometer = new Accelerometer();
                    
                    // Specify the desired time between updates. The sensor accepts
                    // intervals in multiples of 20 ms.
                    accelerometer.TimeBetweenUpdates = TimeSpan.FromMilliseconds(20);
                    accelerometer.CurrentValueChanged += new EventHandler<SensorReadingEventArgs<AccelerometerReading>>(accelerometer_CurrentValueChanged);
                }

                try
                {
                    statusbox.Text = "Starting accelerometer";
                    accelerometer.Start();
                    timer.Start();
                }
                catch (InvalidOperationException)
                {
                    statusbox.Text = "Unable to start accelerometer";
                }
            }

            // COMPASS ON/OFF
            if(Compass.IsSupported){

                if (compass != null && compass.IsDataValid)
                {
                    // Stop data acquisition from the compass.
                    compass.Stop();
                    timer.Stop();
                    //statusbox.Text = "Compass stopped";     
                    statusbox.Text = "Stopped";
                
                }
                else
                {
                    if (compass == null /*&& Compass.IsSupported*/)
                    {
                        // Instantiate the compass.
                        compass = new Compass();

                        // Specify the desired time between updates. The sensor accepts
                        // intervals in multiples of 20 ms.
                        compass.TimeBetweenUpdates = TimeSpan.FromMilliseconds(20);

                        // The sensor may not support the requested time between updates.
                        // The TimeBetweenUpdates property reflects the actual rate.

                        compass.CurrentValueChanged += new EventHandler<SensorReadingEventArgs<CompassReading>>(compass_CurrentValueChanged);
                        compass.Calibrate += new EventHandler<CalibrationEventArgs>(compass_Calibrate);
                    }

                    try
                    {
                        statusbox.Text = "Starting compass";
                        compass.Start();
                        timer.Start();                        
                    }
                    catch (InvalidOperationException)
                    {
                        statusbox.Text = "Unable to start compass";
                    }
                }


            }
        }
        
        // Code to refetch image in map when zoom level is changed -Philippos
        // (WP8 Bing maps doesn't support zooming of attached map layers by default)
        private void map_ZoomLevelChanged(object sender, MapZoomLevelChangedEventArgs e)
        {
            double zoom = map.ZoomLevel;
            map.Layers.Clear();

            MapLayer myLayer = new MapLayer();
            MapOverlay myOverlay = new MapOverlay()
            {
                GeoCoordinate = new GeoCoordinate(35.144925, 33.410800)
            };
            
            ExpanderView expander = new ExpanderView();

            double Width = prevWidth * Math.Pow(2, zoom - prevZoom);  
            expander.Header = new Image()
            {
                Source = new BitmapImage(new Uri("/images/2.png", UriKind.Relative)),
                Width = Width
            };

            myOverlay.Content = expander;           
            myLayer.Add(myOverlay);            
            
            map.Layers.Add(myLayer);
            
            prevWidth = Width;
            prevZoom = zoom;     

        }

    }
}