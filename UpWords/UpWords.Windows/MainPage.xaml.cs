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
        }

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			if (e.Parameter is App)
			{
				m_parentClass = e.Parameter as App;

				m_parentClass.NetworkCommunications.OnGamePostReceived -= NetworkCommunications_OnGamePostReceived;
				m_parentClass.NetworkCommunications.OnGamePostReceived += NetworkCommunications_OnGamePostReceived;

				m_parentClass.NetworkCommunications.Start();
			}
		}

		void NetworkCommunications_OnGamePostReceived(string ipAddress, string playerName)
		{
			if(!m_knownGames.ContainsKey(ipAddress))
			{
				m_knownGames.Add(ipAddress, playerName);
				AvailableGamesListView.Items.Add(playerName);
			}
		}

		private void CreateGameButton_Click(object sender, RoutedEventArgs e)
		{
			if (!m_knownGames.ContainsKey(m_parentClass.NetworkCommunications.CurrentIPAddress()))
			{
				if (m_parentClass != null)
				{
					string ipAddress = m_parentClass.NetworkCommunications.CurrentIPAddress();
					string playerName = m_parentClass.NetworkCommunications.CurrentMachineName();

					m_parentClass.NetworkCommunications.PostGame(ipAddress, playerName);
					m_knownGames.Add(ipAddress, playerName);
					AvailableGamesListView.Items.Add(playerName);
				}
			}
		}

		private void StartOrJoinGameButton_Click(object sender, RoutedEventArgs e)
		{
			Frame.Navigate(typeof(GamePage), false);
		}

		private void ResumeGameButton_Click(object sender, RoutedEventArgs e)
		{

		}
    }
}
