using System;
using System.ComponentModel;
using System.Timers;
using System.Windows;
using WiimoteLib;

namespace WiiScaleWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Wiimote bb = null;
        ConnectionManager cm = null;
        Timer BoardTimer = null;
        float ZeroedWeight = 0;
        float[] History = new float[100];
        int HistoryBest = 1, HistoryCursor = -1;
        string StarFull = "", StarEmpty = "";

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Shutdown();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            StarFull = lblQuality.Text.Substring(0, 1);
            StarEmpty = lblQuality.Text.Substring(4, 1);
            lblWeight.Text = "";
            lblQuality.Text = "";
            lblUnit.Content = "";

            ConnectBalanceBoard(false);

            BoardTimer = new Timer();
            BoardTimer.Interval = 50;
            BoardTimer.Elapsed += BoardTimer_Elapsed;
            BoardTimer.Start();
        }

        private void BoardTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
                {
                    if (cm != null)
                    {
                        if (cm.IsRunning())
                        {
                            lblWeight.Text = "WAIT...";
                            lblQuality.Text = (lblQuality.Text.Length >= 5 ? "" : lblQuality.Text) + "6";
                            return;
                        }
                        if (cm.HadError())
                        {
                            BoardTimer.Stop();
                            MessageBox.Show("No compatible bluetooth adapter found - Quitting", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            Shutdown();
                            return;
                        }
                        ConnectBalanceBoard(true);
                        return;
                    }

                    float kg = bb.WiimoteState.BalanceBoardState.WeightKg, HistorySum = 0.0f, MaxHist = kg, MinHist = kg, MaxDiff = 0.0f;
                    if (kg < -200)
                    {
                        ConnectBalanceBoard(false);
                        return;
                    }

                    HistoryCursor++;
                    History[HistoryCursor % History.Length] = kg;
                    for (HistoryBest = 0; HistoryBest < History.Length; HistoryBest++)
                    {
                        float HistoryEntry = History[(HistoryCursor + History.Length - HistoryBest) % History.Length];
                        if (System.Math.Abs(MaxHist - HistoryEntry) > 1.0f)
                        {
                            break;
                        }

                        if (System.Math.Abs(MinHist - HistoryEntry) > 1.0f)
                        {
                            break;
                        }

                        if (HistoryEntry > MaxHist)
                        {
                            MaxHist = HistoryEntry;
                        }

                        if (HistoryEntry > MinHist)
                        {
                            MinHist = HistoryEntry;
                        }

                        float Diff = System.Math.Max(System.Math.Abs(HistoryEntry - kg), System.Math.Abs((HistorySum + HistoryEntry) / (HistoryBest + 1) - kg));
                        if (Diff > MaxDiff)
                        {
                            MaxDiff = Diff;
                        }

                        if (Diff > 1.0f)
                        {
                            break;
                        }

                        HistorySum += HistoryEntry;
                    }

                    kg = HistorySum / HistoryBest - ZeroedWeight;

                    float accuracy = 1.0f / HistoryBest;
                    kg = (float)System.Math.Floor(kg / accuracy + 0.5f) * accuracy;

                    if (UsePounds)
                    {
                        kg *= 2.20462262f;
                    }

                    lblWeight.Text = (kg >= 0.0f && kg < 10.0f ? " " : "") + kg.ToString("0.00") + (kg <= -10.0f ? "" : "0");

                    lblQuality.Text = "";
                    for (int i = 0; i < 5; i++)
                    {
                        lblQuality.Text += (i < ((HistoryBest + 5) / (History.Length / 5)) ? StarFull : StarEmpty);
                    }

                    lblUnit.Content = (UsePounds ? "lbs" : "kg");
                });
        }

        private void NewBackgroundWork_Start(object sender, DoWorkEventArgs e)
        {
            throw new NotImplementedException();
        }

        void Shutdown()
        {
            if (BoardTimer != null) { BoardTimer.Stop(); BoardTimer = null; }
            if (cm != null) { cm.Cancel(); cm = null; }
        }

        void ConnectBalanceBoard(bool WasJustConnected)
        {
            bool Connected = true; try { bb = new Wiimote(); bb.Connect(); bb.SetLEDs(1); bb.GetStatus(); } catch { Connected = false; }

            if (!Connected || bb.WiimoteState.ExtensionType != ExtensionType.BalanceBoard)
            {
                if (ConnectionManager.ElevateProcessNeedRestart()) { Shutdown(); return; }
                if (cm == null)
                {
                    cm = new ConnectionManager();
                }

                cm.ConnectNextWiiMote();
                return;
            }
            if (cm != null) { cm.Cancel(); cm = null; }

            lblWeight.Text = "...";
            lblQuality.Text = "";
            lblUnit.Content = "";

            ZeroedWeight = 0.0f;
            int InitWeightCount = 0;
            for (int CountMax = (WasJustConnected ? 100 : 50); InitWeightCount < CountMax || bb.WiimoteState.BalanceBoardState.WeightKg == 0.0f; InitWeightCount++)
            {
                if (bb.WiimoteState.BalanceBoardState.WeightKg < -200)
                {
                    break;
                }

                ZeroedWeight += bb.WiimoteState.BalanceBoardState.WeightKg;
                bb.GetStatus();
            }
            ZeroedWeight /= InitWeightCount;

            //start with half full quality bar
            HistoryCursor = HistoryBest = History.Length / 2;
            for (int i = 0; i < History.Length; i++)
            {
                History[i] = (i > HistoryCursor ? float.MinValue : ZeroedWeight);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            float HistorySum = 0.0f;
            for (int i = 0; i < HistoryBest; i++)
            {
                HistorySum += History[(HistoryCursor + History.Length - i) % History.Length];
            }

            ZeroedWeight = HistorySum / HistoryBest;
        }

        private void LblUnit_Click(object sender, RoutedEventArgs e)
        {
            UsePounds = !UsePounds;
        }

        bool UsePounds = false;

        public MainWindow()
        {
            InitializeComponent();
        }
    }
}
