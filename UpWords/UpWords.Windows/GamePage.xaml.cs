using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UpWords
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class GamePage : Page
	{
		private App m_parentClass = null;
		private DateTime m_messageDisplayTime = DateTime.Now;
		private UpwordsGame m_gameInstance = null;
		private DispatcherTimer m_gameTimer = new DispatcherTimer();
		

		public GamePage()
		{
			this.InitializeComponent();

			m_gameTimer.Tick += GameTimer_Tick;
			m_gameTimer.Interval = new TimeSpan(0, 0, 1);
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			if (e.Parameter is App)
			{
				m_parentClass = e.Parameter as App;

				m_gameInstance = new UpwordsGame(TilePanel, UpWordsBoard, TileCanvas, m_parentClass.NetworkCommunications);
				m_gameInstance.OnShowMessage += GameInstance_OnScreenMessage;
				m_gameInstance.OnChangingALetter += GameInstance_OnChangingALetter;
				m_gameInstance.OnDisplayPlays += GameInstance_OnDisplayPlays;
				m_gameInstance.TurnIndicator += GameInstance_TurnIndicator;
				m_gameInstance.LetterRemaining += GameInstance_LetterRemaining;

				m_gameInstance.InitialiseGame();

				m_gameTimer.Start();
			}
			else
			{
				MessageTextBox.Text = "Failed to start!!";
			}
		}

		private void MainGrid_Loaded(object sender, RoutedEventArgs e)
		{
			//m_gameInstance.StartNewGame();
		}

		private void UpWordsBoard_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			m_gameInstance.SizeChanged();
		}
		
		void GameTimer_Tick(object sender, object e)
		{
			if (!m_gameInstance.ChangingALetter && 
				DateTime.Now.Subtract(m_messageDisplayTime).TotalSeconds > 5 && MessageTextBox.Visibility == Windows.UI.Xaml.Visibility.Visible)
			{
				HideMessage();
			}
		}

		void GameInstance_OnScreenMessage(string message)
		{
			ShowMessage(message);
		}

		void GameInstance_OnChangingALetter(bool changingALetter)
		{
			ChangeLetterButton.Content = "Change a Letter";
			HideMessage();
		}

		void GameInstance_OnDisplayPlays(Windows.UI.Xaml.Documents.Paragraph title, Windows.UI.Xaml.Documents.Paragraph detail)
		{
			PlaysRichTextBlock.Blocks.Insert(0, detail);
			PlaysRichTextBlock.Blocks.Insert(0, title);
		}

		void GameInstance_TurnIndicator(string message)
		{
			TurnIndicatorTextBlock.Text = message;
			SubmitButton.IsEnabled = m_gameInstance.IsYouTurn;
			ChangeLetterButton.IsEnabled = m_gameInstance.IsYouTurn;
			RecallLettersButton.IsEnabled = m_gameInstance.IsYouTurn;
			SkipTurnButton.IsEnabled = m_gameInstance.IsYouTurn;
		}

		void GameInstance_LetterRemaining(string message)
		{
			TileRemainingTextBlock.Text = message;
		}

		private void ShowMessage(string message)
		{
			MessageTextBox.Text = message;
			MessageTextBox.Visibility = Windows.UI.Xaml.Visibility.Visible;
			m_messageDisplayTime = DateTime.Now;
		}

		private void HideMessage()
		{
			MessageTextBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
		}
		

		#region Button events.

		private void SubmitButton_Click(object sender, RoutedEventArgs e)
		{
			HideMessage();

			m_gameInstance.SubmitWords();
		}

		private void ShuffleLettersButton_Click(object sender, RoutedEventArgs e)
		{
			HideMessage();

			m_gameInstance.ShuffleLetters();
		}

		private void ChangeLetterButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_gameInstance.ChangingALetter)
			{
				ChangeLetterButton.Content = "Change a Letter";
				m_gameInstance.ChangingALetter = false;
				HideMessage();
			}
			else
			{
				ChangeLetterButton.Content = "Cancel Change";
				m_gameInstance.ChangingALetter = true;
				ShowMessage("Select the letter you wish to exchange.\r\nThis will end your turn.");
			}
		}

		private void RecallLettersButton_Click(object sender, RoutedEventArgs e)
		{
			m_gameInstance.RecallLetters();
		}

		private void SkipTurnButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void ResignGameButton_Click(object sender, RoutedEventArgs e)
		{

		}

		#endregion Button events.

		private void MessageTextBox_Tapped(object sender, TappedRoutedEventArgs e)
		{
			MessageTextBox.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
		}

	}
}
