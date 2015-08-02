using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;


// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace UpWords
{
	public sealed partial class TileControl : UserControl
	{
		public int GridX { get; set; }
		public int GridY { get; set; }
		public int Layer 
		{ 
			get { return m_layer; }
			set
			{
				m_layer = value;
				if (m_layer < 1)
				{
					TileLayer.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				}
				else
				{
					TileLayer.Text = (m_layer + 1).ToString();
					TileLayer.Visibility = Windows.UI.Xaml.Visibility.Visible;
				}
			}
		}

		public string Letter { get; set; }
		
		public eTileState TileStatus 
		{
			get { return m_tileStatus; }
			set
			{
				m_tileStatus = value;
				switch(m_tileStatus)
				{
					case eTileState.Unknown:
					case eTileState.Played:
					case eTileState.OnPlayerPanel:
					case eTileState.OnComputerPanel:
					case eTileState.InBag:
						TileOverlay.Background = m_transparent;
						break;
					case eTileState.JustPlayed:
						TileOverlay.Background = m_justPlayed;
						break;
					case eTileState.ComposingNewWord:
						TileOverlay.Background = m_highlighted;
						break;
				}
			}
		}

		private int m_layer = 0;
		private eTileState m_tileStatus = eTileState.Unknown;
		private SolidColorBrush m_transparent = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
		private SolidColorBrush m_justPlayed = new SolidColorBrush(Color.FromArgb(50, 0, 255, 0));
		private SolidColorBrush m_highlighted = new SolidColorBrush(Color.FromArgb(50, 255, 0, 0));

		public TileControl(string letter)
		{
			this.InitializeComponent();

			Letter = letter.ToUpper();
			GridX = -1;
			GridY = -1;
			Layer = 0;
			TileStatus = eTileState.InBag;

			TileLetter.Text = letter;
		}
	}
}
