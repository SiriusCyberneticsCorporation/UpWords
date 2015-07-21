using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
		public int Layer { get; set; }
		public string Letter { get; set; }
		public eTileState TileStatus { get; set; }

		public TileControl(string letter)
		{
			this.InitializeComponent();

			Letter = letter;
			GridX = -1;
			GridY = -1;
			Layer = 0;
			TileStatus = eTileState.InBag;

			string letterFile = string.Format("ms-appx:///Assets/{0}.png", Letter);
			LetterImage.Source = new BitmapImage() { UriSource = new Uri(letterFile, UriKind.Absolute) };
		}
	}
}
