using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Xceed.Wpf.Toolkit;
using System.IO;
using System.IO.Ports;
using System.Diagnostics;
using System.Windows.Threading;
using System.Threading.Tasks;
using ScottPlot.plottables;
using ScottPlot;

namespace uSMU_FrontPanel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SerialPort sp = new SerialPort();
        public string[] ports = SerialPort.GetPortNames();

        bool continuous_update = true;
        bool replyRecieved = false;
        bool smu_connected = false;

        float requested_voltage;

        public double[] timestampdata = new double[100000];
        public double[] currentdata = new double[100000];
        public double[] voltagedata = new double[100000];

        public int nextDataIndex = 0;
        public int voltagenextDataIndex = 0;

        Stopwatch stopwatch = new Stopwatch();

        PlottableSignalXY currentPlot;
        PlottableSignalXY voltagePlot;

        public MainWindow()
        {
            InitializeComponent();
            portsbox.ItemsSource = ports;

            currentPlot = iPlot.plt.PlotSignalXY(timestampdata, currentdata, color: System.Drawing.Color.DarkOrange, lineWidth: 2);
            iPlot.plt.YLabel("Current (A)");
            iPlot.plt.XLabel("Time (s)");

            voltagePlot = vPlot.plt.PlotSignalXY(timestampdata, voltagedata, color: System.Drawing.Color.DodgerBlue, lineWidth: 2);
            vPlot.plt.YLabel("Voltage (V)");
            vPlot.plt.XLabel("Time (s)");

            DispatcherTimer renderTimer = new DispatcherTimer();
            renderTimer.Interval = TimeSpan.FromMilliseconds(20);
            renderTimer.Tick += Render;
            renderTimer.Start();

        }

        private void Portsbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string portName = portsbox.Text;
                sp.PortName = portName;
                sp.BaudRate = 115200;
                sp.DtrEnable = true;
                sp.RtsEnable = true;
                sp.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                sp.Open();
                smu_connected = true;

            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("Connection failed. Is the correct port selected?");
            }
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            smu_connected = false;
            stopwatch.Stop();
        }

        private void ampEn_Click(object sender, RoutedEventArgs e)
        {
            string send = String.Format("CH1:ENA");
            sp.WriteLine(send);
            Debug.Print("SMU enable");

            int requested_osr = Int32.Parse(osrSlider.Value.ToString());
            send = String.Format("CH1:OSR {0}", requested_osr);
            sp.WriteLine(send);
            Debug.Print("OSR set to {0}", requested_osr);

            stopwatch.Reset();
            stopwatch.Start();

            continuous_update = true;

            Dispatcher.Invoke(() =>
            {
                singleShotStream();
            });
        }

        private void ampDis_Click(object sender, RoutedEventArgs e)
        {
            string send = String.Format("CH1:DIS");
            sp.WriteLine(send);
            Debug.Print("SMU disable");
            continuous_update = false;

        }

        private void voltageBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            requested_voltage = Convert.ToSingle(setVoltageTextBox.Value);
            Debug.Print("requested_voltage changed to: {0}", requested_voltage);


        }
        private void currentLimitTextBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {

            if (smu_connected)
            {
                float requested_current_limit = Convert.ToSingle(currentLimitTextBox.Value);
                string send = String.Format("CH1:CUR {0}", requested_current_limit);
                sp.WriteLine(send);
                Debug.Print("OSR set to {0}", requested_current_limit);
            }
        }

        private void osrBox_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {

        }

        private void osrSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (smu_connected)
            {
                int requested_osr = Int32.Parse(osrSlider.Value.ToString());
                string send = String.Format("CH1:OSR {0}", requested_osr);
                sp.WriteLine(send);
                Debug.Print("OSR set to {0}", requested_osr);
            }
        }

        private void clearGraphButton_Click(object sender, RoutedEventArgs e)
        {

            if (voltagedata != null)
            {
                Array.Clear(voltagedata, 0, voltagedata.Length);
                Array.Clear(currentdata, 0, currentdata.Length);
                Array.Clear(timestampdata, 0, timestampdata.Length);

            }
        }

        private void export_Click(object sender, RoutedEventArgs e)
        {

            String filename = "";

            // Configure save file dialog box
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "uSMU ";
            dlg.FileName += DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"); // Default file name
            dlg.DefaultExt = ".csv"; // Default file extension
            dlg.Filter = "Comma-separated values|*.csv"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                filename = dlg.FileName;
            }
            else
            {
                return;
            }


            string dataout = filename;

            double[] timestampexport = new double[nextDataIndex];
            double[] voltageexport = new double[nextDataIndex];
            double[] currentexport = new double[nextDataIndex];

            timestampexport = timestampdata;
            voltageexport = voltagedata;
            currentexport = currentdata;

            Array.Resize(ref timestampexport, nextDataIndex);
            Array.Resize(ref voltageexport, nextDataIndex);
            Array.Resize(ref currentexport, nextDataIndex);


            using (StreamWriter file = new StreamWriter(dataout))
            {

                file.WriteLine("Time (s), Voltage (V), Current (A)");


                for (int i = 0; i < nextDataIndex; i++)
                {
                    file.Write(timestampexport[i] + ",");
                    file.Write(voltageexport[i] + ",");
                    file.WriteLine(currentexport[i] + ",");
                }

            }

        }


        void Render(object sender, EventArgs e)
        {
            iPlot.plt.AxisAuto();
            iPlot.Render(skipIfCurrentlyRendering: true);

            vPlot.plt.AxisAuto();
            vPlot.Render(skipIfCurrentlyRendering: true);
        }

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            string inLine = sp.ReadLine();
            //Dispatcher.Invoke(DispatcherPriority.Send, new UpdateUiTextDelegate(UpdateData), inLine);
            Dispatcher.Invoke(() =>
            {
                try
                {
                    UpdateData(inLine);
                    replyRecieved = true;
                }
                catch (Exception ex)
                {
                    //  MessageBox.Show("Error interpreting input data");
                    //  Console.WriteLine("UpdateData error - {0}", ex);
                }
            });
        }

        private async void UpdateData(string inLine)
        {
            Console.WriteLine("Recieved: {0}", inLine);


            string str = inLine.Substring(0, inLine.Length);
            str = str.Replace("\0", string.Empty);

            try
            {
                string[] parts = str.Split(',');

                float voltage = float.Parse(parts[0]);
                if (voltage > 0)
                {
                    voltageTextBlock.Text = String.Format("+{0} V", voltage.ToString("F3"));
                }
                else
                {
                    voltageTextBlock.Text = String.Format("{0} V", voltage.ToString("F3"));
                }

                float current = float.Parse(parts[1]);
                float current_autorange_units = current;


                if (Math.Abs(current) >= 1E-4)
                {
                    current_autorange_units *= (float)1E3;

                    if (current > 0)
                    {
                        currentTextBlock.Text = String.Format("+{0} mA", current_autorange_units.ToString("F3"));
                    }
                    else
                    {
                        currentTextBlock.Text = String.Format("{0:00000} mA", current_autorange_units.ToString("F3"));
                    }

                }

                else if (Math.Abs(current) < 1E-4)
                {
                    current_autorange_units *= (float)1E6;

                    if (current > 0)
                    {
                        currentTextBlock.Text = String.Format("+{0:00000} μA", current_autorange_units.ToString("F1"));
                    }
                    else
                    {
                        currentTextBlock.Text = String.Format("{0:00000} μA", current_autorange_units.ToString("F1"));

                    }
                }

                currentdata[nextDataIndex] = current;
                currentPlot.maxRenderIndex = nextDataIndex;

                voltagedata[nextDataIndex] = voltage;
                voltagePlot.maxRenderIndex = voltagenextDataIndex;

                timestampdata[nextDataIndex] = (double)stopwatch.ElapsedMilliseconds / 1000;

                nextDataIndex += 1;
                voltagenextDataIndex += 1;

            }

            catch (Exception ex)
            {
                Console.WriteLine("Error - Data from SMU not understood. {0}", ex);
            }

        }

        private async void singleShotStream()
        {
            while (continuous_update)
            {
                string send = String.Format("CH1:MEA:VOL {0}", requested_voltage);
                sp.WriteLine(send);
                Debug.Print("Requested voltage: {0}", send);

                while (!replyRecieved)
                {
                    Console.WriteLine("Waiting...");
                    await Task.Delay(10);
                }

                replyRecieved = false;
                //continuous_update = false;
            }

        }


    }


}
