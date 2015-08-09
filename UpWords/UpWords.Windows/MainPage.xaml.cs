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
		private App m_parentClass = null;
		private Dictionary<string, string> m_knownGames = new Dictionary<string, string>();

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
				m_parentClass.NetworkCommunications.OnStartGameReceived -= NetworkCommunications_OnStartGameReceived;

				m_parentClass.NetworkCommunications.OnErrorMessage += NetworkCommunications_OnErrorMessage;
				m_parentClass.NetworkCommunications.OnGameCreatedReceived += NetworkCommunications_OnGameCreatedReceived;
				m_parentClass.NetworkCommunications.OnGameCancelledReceived += NetworkCommunications_OnGameCancelledReceived;
				m_parentClass.NetworkCommunications.OnGameJoinedReceived += NetworkCommunications_OnGameJoinedReceived;
				m_parentClass.NetworkCommunications.OnStartGameReceived += NetworkCommunications_OnStartGameReceived;

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
				m_knownGames.Add(gameInformation.GameTitle, gameInformation.CreatorsIpAddress);

				if (!GameSettings.Settings.GameCreated)
				{
					AvailableGamesListView.Items.Add(gameInformation.GameTitle);
					CreateGameButton.IsEnabled = false;
				}
			}
		}

		void NetworkCommunications_OnStartGameReceived(string serverIP)
		{
			if (GameSettings.Settings.CreatorsIpAddress == serverIP)
			{
				Frame.Navigate(typeof(GamePage), m_parentClass);
			}
		}


		void NetworkCommunications_OnGameCancelledReceived(GameDetails gameInformation)
		{
			m_knownGames.Remove(gameInformation.GameTitle);

			AvailableGamesListView.Items.Remove(gameInformation.GameTitle);

		}

		void NetworkCommunications_OnGameJoinedReceived(string playersIpAddress, PlayerDetails playersDetails)
		{
			GameSettings.Settings.PlayersJoined.Add(playersIpAddress, playersDetails);
			GameSettings.SaveSettings();

			AvailableGamesListView.Items.Add(playersDetails.Name + " on " + playersDetails.Machine);
			StartGameButton.IsEnabled = true;
		}

		private void CreateGameButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_parentClass != null)
			{
				GameSettings.Settings.GameCreated = true;
				GameSettings.Settings.CreatorsIpAddress = m_parentClass.NetworkCommunications.IpAddress;
				GameSettings.Settings.GameTitle = m_parentClass.NetworkCommunications.Username + " on " + m_parentClass.NetworkCommunications.MachineName;
				GameSettings.Settings.PlayersJoined.Clear();
				GameSettings.SaveSettings();

				m_parentClass.NetworkCommunications.CreateGame(GameSettings.Settings.GameTitle);
				//m_knownGames.Add(GameSettings.Settings.GameTitle, GameSettings.Settings.GameCreated);

				AvailableGamesListView.Items.Clear();
				ListTitleTextBlock.Text = "Players Joined";
				CreateGameButton.IsEnabled = false;
				StartGameButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
				StartGameButton.IsEnabled = false;
			}
		}

		private void StartGameButton_Click(object sender, RoutedEventArgs e)
		{
			if (GameSettings.Settings.GameCreated)
			{
				Frame.Navigate(typeof(GamePage), m_parentClass);
			}
		}

		private void JoinGameButton_Click(object sender, RoutedEventArgs e)
		{
			if (AvailableGamesListView.SelectedIndex >= 0)
			{
				string gameTitle = AvailableGamesListView.SelectedItem as string;

				m_parentClass.NetworkCommunications.JoinGame(m_knownGames[gameTitle], GameSettings.Settings.MyDetails);

				GameSettings.Settings.GameCreated = false;
				GameSettings.Settings.CreatorsIpAddress = m_knownGames[gameTitle];
				GameSettings.SaveSettings();
				
				JoinGameButton.IsEnabled = false;
				PlayerMessageTextBlock.Text = "Waiting for " + gameTitle + " to start the game.";
				PlayerMessageTextBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
			}
		}

		private void ResumeGameButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void CancelGameButton_Click(object sender, RoutedEventArgs e)
		{
			if (GameSettings.Settings.GameCreated && AvailableGamesListView.SelectedIndex >= 0)
			{
				string gameInformation = AvailableGamesListView.SelectedItem as string;

				m_parentClass.NetworkCommunications.CancelGame(gameInformation);
				m_knownGames.Remove(gameInformation);

				AvailableGamesListView.Items.Clear();
				ListTitleTextBlock.Text = "Available Games";
				CreateGameButton.IsEnabled = true;
				StartGameButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				GameSettings.Settings.GameCreated = false;
				GameSettings.SaveSettings();
			}
		}

		private void AvailableGamesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (GameSettings.Settings.GameCreated)
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
