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
		public TileDetails TileData { get { return m_tileDetails; } }

		public int GridX { get { return m_tileDetails.GridX; } set { m_tileDetails.GridX = value; } }
		public int GridY { get { return m_tileDetails.GridY; } set { m_tileDetails.GridY = value; } }
		public int Layer 
		{
			get { return m_tileDetails.Layer; }
			set
			{
				m_tileDetails.Layer = value;
				if (m_tileDetails.Layer < 2)
				{
					TileLayer.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				}
				else
				{
					TileLayer.Text = (m_tileDetails.Layer).ToString();
					TileLayer.Visibility = Windows.UI.Xaml.Visibility.Visible;
				}
			}
		}

		public string Letter { get { return m_tileDetails.Letter; } set { m_tileDetails.Letter = value; } }
		
		public eTileState TileStatus 
		{
			get { return m_tileDetails.TileStatus; }
			set
			{
				m_tileDetails.TileStatus = value;
				switch (m_tileDetails.TileStatus)
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

		private TileDetails m_tileDetails = new TileDetails();
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

		public TileControl(TileDetails tileData)
		{
			this.InitializeComponent();

			Letter = tileData.Letter;
			GridX = tileData.GridX;
			GridY = tileData.GridY;
			Layer = tileData.Layer;
			TileStatus = tileData.TileStatus;

			TileLetter.Text = Letter;
		}
	}
}
