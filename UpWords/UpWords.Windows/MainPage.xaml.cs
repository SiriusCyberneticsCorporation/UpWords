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
    public sealed partial class MainPage : Page
    {
		private bool m_gameCreated = false;
		private App m_parentClass = null;
		private GameDetails m_gameJoined = null;
		private Dictionary<string, GameDetails> m_knownGames = new Dictionary<string, GameDetails>();
		private Dictionary<string, string> m_playersJoined = new Dictionary<string, string>();

        public MainPage()
        {
            this.InitializeComponent();
			
			ListTitleTextBlock.Text = "Available Games";			
        }

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			if (e.Parameter is App)
			{
				m_parentClass = e.Parameter as App;

				m_parentClass.NetworkCommunications.OnErrorMessage -= NetworkCommunications_OnErrorMessage;
				m_parentClass.NetworkCommunications.OnGameCreatedReceived -= NetworkCommunications_OnGameCreatedReceived;
				m_parentClass.NetworkCommunications.OnGameCancelledReceived -= NetworkCommunications_OnGameCancelledReceived;
				m_parentClass.NetworkCommunications.OnGameJoinedReceived -= NetworkCommunications_OnGameJoinedReceived;
				m_parentClass.NetworkCommunications.OnGameStartedReceived -= NetworkCommunications_OnGameStartedReceived;

				m_parentClass.NetworkCommunications.OnErrorMessage += NetworkCommunications_OnErrorMessage;
				m_parentClass.NetworkCommunications.OnGameCreatedReceived += NetworkCommunications_OnGameCreatedReceived;
				m_parentClass.NetworkCommunications.OnGameCancelledReceived += NetworkCommunications_OnGameCancelledReceived;
				m_parentClass.NetworkCommunications.OnGameJoinedReceived += NetworkCommunications_OnGameJoinedReceived;
				m_parentClass.NetworkCommunications.OnGameStartedReceived += NetworkCommunications_OnGameStartedReceived;

				m_parentClass.NetworkCommunications.Start();
			}
		}

		void NetworkCommunications_OnErrorMessage(string message)
		{
			ErrorMessageTextBlock.Text = message;
		}

		void NetworkCommunications_OnGameCreatedReceived(GameDetails gameInformation)
		{
			if (!m_knownGames.ContainsKey(gameInformation.GameTitle))
			{
				m_knownGames.Add(gameInformation.GameTitle, gameInformation);

				if (!m_gameCreated)
				{
					AvailableGamesListView.Items.Add(gameInformation.GameTitle);
				}
			}
		}

		void NetworkCommunications_OnGameStartedReceived(GameDetails gameInformation)
		{
			if (m_gameCreated)
			{
				Frame.Navigate(typeof(GamePage), false);
			}
			else if(m_gameJoined.CreatorsIpAddress == gameInformation.CreatorsIpAddress &&
					m_gameJoined.GameTitle == gameInformation.GameTitle)
			{
				Frame.Navigate(typeof(GamePage), false);
			}
		}


		void NetworkCommunications_OnGameCancelledReceived(GameDetails gameInformation)
		{
			m_knownGames.Remove(gameInformation.GameTitle);

			AvailableGamesListView.Items.Remove(gameInformation.GameTitle);

		}

		void NetworkCommunications_OnGameJoinedReceived(string playersIpAddress, string playersDetails)
		{
			m_playersJoined.Add(playersIpAddress, playersDetails);
			
			AvailableGamesListView.Items.Add(playersDetails);
		}

		private void CreateGameButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_parentClass != null)
			{
				GameDetails gameInformation = new GameDetails()
				{
					CreatorsIpAddress = m_parentClass.NetworkCommunications.IpAddress,
					GameTitle = m_parentClass.NetworkCommunications.Username + " on " + m_parentClass.NetworkCommunications.MachineName
				};

				m_parentClass.NetworkCommunications.CreateGame(gameInformation.GameTitle);
				m_knownGames.Add(gameInformation.GameTitle, gameInformation);

				m_playersJoined.Clear();
				AvailableGamesListView.Items.Clear();
				ListTitleTextBlock.Text = "Players Joined";
				CreateGameButton.IsEnabled = false;
				StartGameButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
				m_gameCreated = true;
			}
		}

		private void StartGameButton_Click(object sender, RoutedEventArgs e)
		{
			if(m_gameCreated)
			{
				string gameTitle = m_parentClass.NetworkCommunications.Username + " on " + m_parentClass.NetworkCommunications.MachineName;

				m_parentClass.NetworkCommunications.StartGame(gameTitle);
			}
		}

		private void JoinGameButton_Click(object sender, RoutedEventArgs e)
		{
			if (AvailableGamesListView.SelectedIndex >= 0)
			{
				string gameTitle = AvailableGamesListView.SelectedItem as string;

				m_parentClass.NetworkCommunications.JoinGame(m_knownGames[gameTitle]);
				
				m_gameJoined = m_knownGames[gameTitle];
			}
		}

		private void ResumeGameButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void CancelGameButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_gameCreated && AvailableGamesListView.SelectedIndex >= 0)
			{
				string gameInformation = AvailableGamesListView.SelectedItem as string;

				m_parentClass.NetworkCommunications.CancelGame(gameInformation);
				m_knownGames.Remove(gameInformation);

				AvailableGamesListView.Items.Clear();
				ListTitleTextBlock.Text = "Available Games";
				CreateGameButton.IsEnabled = true;
				StartGameButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				m_gameCreated = false;
			}
		}

		private void AvailableGamesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if(m_gameCreated)
			{
				if(AvailableGamesListView.SelectedIndex >= 0)
				{
					CancelGameButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
				}
				else
				{
					CancelGameButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				}
			}
			else
			{
				if(AvailableGamesListView.SelectedIndex >= 0)
				{
					JoinGameButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
				}
				else
				{
					JoinGameButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				}				
			}
		}
	}
}
