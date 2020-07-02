using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;
using Un4seen.Bass;
using Un4seen.BassWasapi;


namespace rawshed
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}
		private bool _enable;               //calisma durumu
		private DispatcherTimer _t;         //yenilemek icin timer
		private float[] _fft;               //hizli fourier donusumu icin buffer
		//private ProgressBar _l, _r;         //progressbars for left and right channel intensity
		private WASAPIPROC _process;        
		private int _lastlevel;             //son cikis seviyesi
		private int _hanctr;                //son cikis seviyesi sayaci
		private List<byte> _spectrumdata;   //spectrum veri bufferi
		//private Spectrum _spectrum;         //spectrum dispay control
		private ComboBox _devicelist;       //aygit listesi
		private bool _initialized;          //basladi mi
		private int devindex;               //kullanilan aygit

		private int _lines = 16;            // kac spectrum var
		private void Form1_Load(object sender, EventArgs e)
		{
			btnDisable.Enabled = false;
			_fft = new float[1024];
			_lastlevel = 0;
			_hanctr = 0;
			_t = new DispatcherTimer();
			_t.Tick += _t_Tick;
			_t.Interval = TimeSpan.FromMilliseconds(25); //40hz yenileme hizi
			_t.IsEnabled = false;
			_process = new WASAPIPROC(Process);
			_spectrumdata = new List<byte>();
			_devicelist = devicelist;
			_initialized = false;
			Init();

		}

		public void setSpectrum(List<byte> data)
		{
			//MessageBox.Show("DataCount: " + data.Count);
			if (data.Count < 16) return;
			//MessageBox.Show(data[0].ToString());
			bar1.Value = data[0];
			bar2.Value = data[1];
			bar3.Value = data[2];
			bar4.Value = data[3];
			bar5.Value = data[4];
			bar6.Value = data[5];
			bar7.Value = data[6];
			bar8.Value = data[7];
			bar9.Value = data[8];
			bar10.Value = data[9];
			bar11.Value = data[10];
			bar12.Value = data[11];
			bar13.Value = data[12];
			bar14.Value = data[13];
			bar15.Value = data[14];
			bar16.Value = data[15];
		}
		//programi aktif ve deaktif etmek icin flag ayaralayalim
		public bool Enable
		{
			get { return _enable; }
			set
			{
				_enable = value;
				if (value)
				{
					if (!_initialized)
					{
						var str = (_devicelist.Items[_devicelist.SelectedIndex] as string);

						var array = str.Split(' ');
						devindex = Convert.ToInt32(array[0]);
						bool result = BassWasapi.BASS_WASAPI_Init(devindex, 0, 0,
																  BASSWASAPIInit.BASS_WASAPI_BUFFER,
																  1f, 0.05f,
																  _process, IntPtr.Zero);
						if (!result)
						{
							var error = Bass.BASS_ErrorGetCode();
							MessageBox.Show(error.ToString());
						}
						else
						{
							_initialized = true;
							_devicelist.Enabled = false;
						}
					}
					BassWasapi.BASS_WASAPI_Start();
				}
				else BassWasapi.BASS_WASAPI_Stop(true);
				System.Threading.Thread.Sleep(500);
				_t.IsEnabled = value;
			}
		}
		// programi ve kutuphanemizi baslatalim
		private void Init()
		{
			bool result = false;
			for (int i = 0; i < BassWasapi.BASS_WASAPI_GetDeviceCount(); i++)
			{
				var device = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
				if (device.IsEnabled && device.IsLoopback)
				{
					_devicelist.Items.Add(string.Format("{0} - {1}", i, device.name));
				}
			}
			_devicelist.SelectedIndex = 0;
			Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);
			result = Bass.BASS_Init(0, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
			if (!result) throw new Exception("Init Error");
		}

		//timer 
		private void _t_Tick(object sender, EventArgs e)
		{
			// fft alalim (fast fourier transform). hata varsa -1 dondurur.
			int ret = BassWasapi.BASS_WASAPI_GetData(_fft, (int)BASSData.BASS_DATA_FFT2048);
			if (ret < 0) return;
			int x, y;
			int b0 = 0;

			//spectrumun datasini hesaplayalim.
			for (x = 0; x < _lines; x++)
			{
				float peak = 0;
				int b1 = (int)Math.Pow(2, x * 10.0 / (_lines - 1));
				if (b1 > 1023) b1 = 1023;
				if (b1 <= b0) b1 = b0 + 1;
				for (; b0 < b1; b0++)
				{
					if (peak < _fft[1 + b0]) peak = _fft[1 + b0];
				}
				y = (int)(Math.Sqrt(peak) * 3 * 255 - 4);
				if (y > 255) y = 255;
				if (y < 0) y = 0;
				_spectrumdata.Add((byte)y);
			}

			if (_enable) setSpectrum(_spectrumdata);
			_spectrumdata.Clear();


			int level = BassWasapi.BASS_WASAPI_GetLevel();
			if (level == _lastlevel && level != 0) _hanctr++;
			_lastlevel = level;

			//glitch olmamasi icin belli bir asima ugradiysa yeniden baslatalim
			if (_hanctr > 3)
			{
				_hanctr = 0;
				Free();
				Bass.BASS_Init(0, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
				_initialized = false;
				Enable = true;
			}
		}

		// surekli calismasi icin gerekli
		private int Process(IntPtr buffer, int length, IntPtr user)
		{
			return length;
		}

		private void Button1_Click(object sender, EventArgs e)
		{
			Enable = true;
			btnEnable.Enabled = false;
			btnDisable.Enabled = true;

		}

		private void BtnDisable_Click(object sender, EventArgs e)
		{
			Enable = false;
			btnDisable.Enabled = false;
			btnEnable.Enabled = true;
			List<byte> _cop = new List<byte>(new byte[16]);
			setSpectrum(_cop);
		}

		//temizlik
		public void Free()
		{
			BassWasapi.BASS_WASAPI_Free();
			Bass.BASS_Free();
		}
	}
}
