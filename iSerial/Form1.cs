using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Windows.Forms.DataVisualization.Charting;

namespace iSerial
{

    public partial class iSerial : Form
    {
        Random rdm = new Random(); /* 乱数 */

        private int index = 0;
        private int yIndex = 0;

        bool chartOptimized = false;

        List<int> startList = new List<int>();
        List<int> endList = new List<int>();

        List<ChartArea> chartAreas = new List<ChartArea>();
        List<Series> chartSeries = new List<Series>();

        public iSerial()
        {
            InitializeComponent();
            ScanCOMPorts();

            InitCharts();

            InitComboBox();
            InitButton();
        }

        private void InitComboBox()
        {
            cmbNewline.SelectedIndex = 0;
            cmbBaudrate.SelectedIndex = 5;
        }

        private void InitButton()
        {
            /* 起動時ボタンの状態設定 */
            btnOpen.Enabled = false;
            btnClose.Enabled = false;
            btnSend.Enabled = false;
            btnClear.Enabled = false;
        }

        private void InitCharts()
        {
            /* 初期値の削除 */
            chart1.Series.Clear();
            chart1.ChartAreas.Clear();
            chart1.Titles.Clear();
            chart1.Legends.Clear();
        }

        private void ScanCOMPorts()
        {
            cmbCOMPort.Items.Clear();
            cmbCOMPort.Items.Add("Select COM Port");
            cmbCOMPort.SelectedIndex = 0;
            string[] ports = SerialPort.GetPortNames();
            foreach (string p in ports)
            {
                cmbCOMPort.Items.Add(p);
            }
        }

        private void BtnScan_Click(object sender, EventArgs e)
        {
            ScanCOMPorts();
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            try
            {
                /* COMポートを最初に開くときにUIの状態を変える */
                serialPort1.PortName = cmbCOMPort.Text; // COM名設定
                serialPort1.BaudRate = int.Parse(cmbBaudrate.Text); // ボーレート設定
                serialPort1.Open();
                btnOpen.Enabled = false;
                btnClose.Enabled = true;
                btnScan.Enabled = false;
                btnClear.Enabled = true;
                cmbCOMPort.Enabled = false;
                cmbBaudrate.Enabled = false;
                btnSend.Enabled = true;
                tbxRxData.Clear();
                tbxRxData.AppendText("Connected\r\n");
            }
            catch
            {
                BtnClose_Click(this, null); //ミスった時には起動しない
            }
        }

        // TODO: Chartとの組み合わせでうまく動いていない部分があるっぽい
        private void BtnClose_Click(object sender, EventArgs e)
        {
            /* シリアルポートを閉じたときのUIの状態変更 */
            btnOpen.Enabled = true;
            btnClose.Enabled = false;
            btnScan.Enabled = true;
            cmbCOMPort.Enabled = true;
            cmbBaudrate.Enabled = true;
            btnSend.Enabled = false;
            try
            {
                serialPort1.DiscardInBuffer(); // 入力バッファを破棄
                serialPort1.DiscardOutBuffer(); // 出力バッファを破棄
                serialPort1.Close(); // COMポートを閉じる
            }
            catch { };
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            try
            {
                serialPort1.Write(tbxTxData.Text);
                if(cmbNewline.Text == "CRLF") serialPort1.Write("\r\n");
                else if(cmbNewline.Text == "LF") serialPort1.Write("\n");
                else serialPort1.Write("");
            }
            catch
            {
                BtnClose_Click(this, null);
            }
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            tbxRxData.Clear();
        }

        private void SerialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (serialPort1.BytesToRead < 2)
            {
                return;
            }

            string recievedData;
            if(!serialPort1.IsOpen) return;
            try {
                recievedData = serialPort1.ReadLine();
            }
            catch
            {
                return ;
            }
            index++;/* 読み込みまで出来たら番号を増やす */
            try
            {
                SetText(recievedData);
            }catch{
                BtnClose_Click(this, null);
            }

            try
            {
                string[] splitedData = recievedData.Replace("\r", "").Replace("\n", "").Split(',');

                /* 初期化がまだの場合、ここで文解析 */
                if (index == 3 && !chartOptimized)
                {
                    System.Diagnostics.Debug.WriteLine("data for optimize");
                    System.Diagnostics.Debug.WriteLine(recievedData);
                    OptimizeCharts(splitedData);
                }else if (chartOptimized) {
                    UpdateCharts2(splitedData);
                }
            }
            catch
            {
                return ;
            }

        }

        delegate void SetTextCallback(string text);
        private void SetText(string text)
        {
            if (tbxRxData.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                BeginInvoke(d, new object[] { text });
            }
            else
            {
                tbxRxData.AppendText(text+"\n");
            }
        }

        delegate void UpdateCharts2Callback(string[] splitedData);
        private void UpdateCharts2(string[] splitedData)
        {
            if (chart1.InvokeRequired)
            {
                UpdateCharts2Callback d = new UpdateCharts2Callback(UpdateCharts2);
                BeginInvoke(d, new object[] { splitedData });
            }
            else
            {
                int currentChartArea = -1;
                int currentSeries = -1;
                bool yIndexUpdated = false;
                foreach(var val in splitedData)
                {
                    if (val.Equals("<"))
                    {
                        currentChartArea++;
                    }
                    else if (val.Equals(">"))
                    {
                        /* 位置指定（x軸 300個） */
                        try {
                            chart1.ChartAreas["cArea" + currentChartArea.ToString()].AxisX.Minimum = yIndex - 300;
                            chart1.ChartAreas["cArea" + currentChartArea.ToString()].AxisX.Maximum = yIndex;
                        }
                        catch
                        {

                        }
                    }
                    else if (currentChartArea != -1) /* -1だとareaがないのでスルー */
                    {
                        currentSeries++;
                        try {
                            chartSeries[currentSeries].Points.Add(new DataPoint(yIndex, Convert.ToDouble(val)));
                        }
                        catch
                        {

                        }
                        if(!yIndexUpdated){
                            yIndex++; /* チャートの分だけ++されるので最初の1回だけ */
                            yIndexUpdated = true;
                        }
                        /* chartSeriesの数が十万とかになると重いので1000以上になったら最初のデータを消していく */
                        /* このため、chartSeries[currentSeries]は1000このデータ配列（[0]が最古、[999]が最新） */
                        if (yIndex >= 1000) chartSeries[currentSeries].Points.RemoveAt(0);
                    }
                    else
                    {
                    }
                }
                //seriesLine.Points.Add(new DataPoint(splitedData);
            }
        }

        delegate void OptimizeChartsCallback(string[] splitedData);

        private void OptimizeCharts(string[] splitedData)
        {
            if (chart1.InvokeRequired)
            {
                System.Diagnostics.Debug.WriteLine("OPT2");
                OptimizeChartsCallback d = new OptimizeChartsCallback(OptimizeCharts);
                Invoke(d, new object[] { splitedData });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("OPT");
                /* <と>がどれだけあるかの判別 */
                int num = -1;
                foreach (string s in splitedData)
                {
                    num++;
                    if(s.Equals("<")) startList.Add(num);
                    if(s.Equals(">")) endList.Add(num);
                }
                if (startList.Count != endList.Count) chartOptimized =  false; /* <と>の数が違うとき */

                int currentChartArea = -1;

                /* <>の数に併せてchartを作成 */
                foreach (var val in splitedData) {
                    System.Diagnostics.Debug.WriteLine(val);
                    if (val.Equals("<")) {
                        currentChartArea++;
                        ChartArea cA = new ChartArea("cArea" + currentChartArea.ToString());

                        /* グラフ周りの余白設定 */
                        cA.InnerPlotPosition.Auto = true;
                        cA.InnerPlotPosition.Width = 100; // 100%
                        cA.InnerPlotPosition.Height = 85;  // 90%(横軸のメモリラベル印字分の余裕を設ける)
                        cA.InnerPlotPosition.X = 2; // 左側の空白の大きさ
                        cA.InnerPlotPosition.Y = 3; // 上側の空白の大きさ

                        /* 軸の表記設定 */
                        Action<Axis> setAxis = (axisInfo) => {
                            /* 軸メモリのフォントサイズ */
                            axisInfo.LabelAutoFitMaxFontSize = 8;
                            /* 軸メモリの文字色 */
                            axisInfo.LabelStyle.ForeColor = Color.Black;
                            /* 軸タイトルの文字色 */
                            axisInfo.TitleForeColor = Color.Black;
                            /* 網掛けの色 */
                            axisInfo.MajorGrid.Enabled = true;
                            axisInfo.MinorGrid.Enabled = false;
                        };

                        /* 罫線の設定 */
                        cA.AxisX.LineColor = Color.Black; // x軸
                        cA.AxisX.MajorGrid.LineColor = Color.FromArgb(30, 0, 0, 0); // x軸罫線
                        cA.AxisY.LineColor = Color.Black; // y軸
                        cA.AxisY.MajorGrid.LineColor = Color.FromArgb(30, 0, 0, 0); // y軸罫線

                        /* X,Y軸の表示方法を定義 */
                        setAxis(cA.AxisY);
                        setAxis(cA.AxisX);

                        /* チャートを追加 */
                        chart1.ChartAreas.Add(cA);
                    }
                    else if (val.Equals(">"))
                    {
                        /* 閉じかっこの時（何もしない） */
                    }
                    else if(currentChartArea != -1 && val != "") /* -1だとareaがないのでスルー */
                    {
                        var s = new Series();
                        s.ChartType = SeriesChartType.Line;
                        s.LegendText = "Legend:Line";
                        s.ChartArea = "cArea" + currentChartArea.ToString();
                        s.BorderWidth = 2;
                        chartSeries.Add(s);
                        chart1.Series.Add(s);
                        System.Diagnostics.Debug.WriteLine(chartSeries.Count);
                    }
                    else
                    {
                    }
                }
                if ((currentChartArea+1)*2 + chartSeries.Count == splitedData.Length) {
                    chartOptimized = true;
                }
            }
        }

        private void Chart1_Click(object sender, EventArgs e)
        {
            //pass
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //pass
        }

        private void cmbBaudrate_SelectedIndexChanged(object sender, EventArgs e)
        {
            //pass
        }

        private void cmbCOMPort_SelectedIndexChanged(object sender, EventArgs e)
        {
            /* ボタンのUI変更 */
            btnOpen.Enabled = true;
        }
    }
}
