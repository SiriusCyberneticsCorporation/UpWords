using System;
using System.Collections.Generic;
using System.Text;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.ApplicationModel;

namespace UpWords
{
    public class UpwordsGame
    {
		public delegate void ShowMessageHandler(string message);
		public event ShowMessageHandler OnShowMessage;
		public event ShowMessageHandler TurnIndicator;
		public event ShowMessageHandler LetterRemaining;

//		public delegate void HideMessageHandler();
//		public event HideMessageHandler OnHideMessage;

		public delegate void LetterChangingHandler(bool changingALetter);
		public event LetterChangingHandler OnChangingALetter;

		public delegate void ShowPlaysHandler(Paragraph title, Paragraph detail);
		public event ShowPlaysHandler OnDisplayPlays;

		public bool IsYouTurn { get { return m_turnState == eTurnState.PlayersTurn; } }
		public bool ChangingALetter { get; set; }
		public List<TileControl> PanelTiles { get { return m_panelTiles; } }
		

		private const int MAX_TILE_ON_SPACE = 5;
		private const int GRID_SIZE = 10;
		private const int ON_PANEL = 99;

		private bool m_firstWord = true;
		private bool[] m_panelSpaceFilled = new bool[7];
		private int[,] m_boardSpaceFilled = new int[GRID_SIZE, GRID_SIZE];
		private double m_tileSize = 20;
		private Random m_randomiser = null;
		private DateTime m_gameStartTime = DateTime.Now;
		private eTurnState m_turnState = eTurnState.Unknown;
		private CurrentGame m_currentGame = new CurrentGame();
		private PlayerDetails m_currentActivePlayer = null;
		private TileControl[,] m_boardTiles = new TileControl[GRID_SIZE, GRID_SIZE];

		private IList<string> m_words = new List<string>();

		private List<TileControl> m_currentWordTiles = new List<TileControl>();
		private List<TileControl> m_playedTiles = new List<TileControl>();
		private List<TileControl> m_justPlayedTiles = new List<TileControl>();
		private List<TileControl> m_panelTiles = new List<TileControl>();
		private List<TileControl> m_computersTiles = new List<TileControl>();
		private Dictionary<string, PlayerDetails> m_activePlayer = new Dictionary<string, PlayerDetails>();

		private TileControl m_tileBeingExchanged = null;

		private Grid m_tilePanel = null;
		private Image m_gameBoard = null;
		private Canvas m_tileCanvas = null;
		private UpwordsNetworking m_localNetwork = null;

		private enum ePlacementMode
		{
			Redrawing,
			UserPlacement,
			PlacingOpponentTile
		}

		public UpwordsGame(Grid tilePanel, Image gameBoard, Canvas tileCanvas, UpwordsNetworking localNetwork)
		{
			m_tilePanel = tilePanel;
			m_gameBoard = gameBoard;
			m_tileCanvas = tileCanvas;
			m_localNetwork = localNetwork;

			m_localNetwork.OnReadyToStartReceived += LocalNetwork_OnReadyToStartReceived;
			m_localNetwork.OnSetActivePlayerReceived += LocalNetwork_OnSetActivePlayerReceived;
			m_localNetwork.OnPlayersTurnDetailsReceived += LocalNetwork_OnPlayersTurnDetailsReceived;
			m_localNetwork.OnLettersReceived += LocalNetwork_OnLettersReceived;
			m_localNetwork.OnLetterExchange += LocalNetwork_OnLetterExchange;

			m_randomiser = new Random((int)DateTime.Now.TimeOfDay.TotalSeconds);

			ReadWords();
		}

		public void InitialiseGame()
		{
			if (GameSettings.Settings.GameStarted)
			{
				AddLettersToPanel(GameSettings.Settings.MyDetails.CurrentLetters);

				m_activePlayer.Add(m_localNetwork.IpAddress, GameSettings.Settings.MyDetails);

				foreach (string playerIP in GameSettings.Settings.PlayersJoined.Keys)
				{
					m_localNetwork.StartGame(playerIP);
					m_activePlayer.Add(playerIP, GameSettings.Settings.PlayersJoined[playerIP]);
				}
			}
			else
			{
				GameSettings.Settings.MyDetails.ID = Guid.NewGuid();
				GameSettings.Settings.MyDetails.IpAddress = m_localNetwork.IpAddress;
				GameSettings.Settings.MyDetails.Machine = m_localNetwork.MachineName;
				GameSettings.Settings.MyDetails.Name = m_localNetwork.Username;
				GameSettings.Settings.GameStarted = true;
				GameSettings.SaveSettings();

				if (GameSettings.Settings.IamGameCreator)
				{
					FillLetterBag();
					AddLettersToPanel(GetLetters(7));

					m_activePlayer.Add(m_localNetwork.IpAddress, GameSettings.Settings.MyDetails);

					foreach (string playerIP in GameSettings.Settings.PlayersJoined.Keys)
					{
						m_localNetwork.StartGame(playerIP);
						m_activePlayer.Add(playerIP, GameSettings.Settings.PlayersJoined[playerIP]);
					}

					SetStartPlayer();
				}
				else
				{
					// Let the game controller know we are ready to start.
					m_localNetwork.SendReadyToStart(GameSettings.Settings.CreatorsIpAddress);
				}
			}
		}

		private async void ReadWords()
		{
			StorageFile wordsFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Words.txt", UriKind.Absolute));
			m_words = await FileIO.ReadLinesAsync(wordsFile);
		}

		private void SendLetters(string playersIpAddress, int numberOfLetters)
		{
			if(GameSettings.Settings.PlayersJoined.ContainsKey(playersIpAddress))
			{
				GameLetters newLetters = GetLetters(numberOfLetters);
				GameSettings.Settings.PlayersJoined[playersIpAddress].CurrentLetters.Letters.AddRange(newLetters.Letters);
				m_localNetwork.SendLetters(playersIpAddress, newLetters);
			}
			GameSettings.SaveSettings();
		}

		void LocalNetwork_OnReadyToStartReceived(string playersIpAddress)
		{
			SendLetters(playersIpAddress, 7);
			SendActivePlayer();
		}

		void LocalNetwork_OnSetActivePlayerReceived(PlayerDetails activePlayer)
		{
			SetActivePlayer(activePlayer);
		}

		void LocalNetwork_OnPlayersTurnDetailsReceived(string playersIpAddress, PlayersTurnDetails iPlayersTurnDetails)
		{
			DisplayPlay(iPlayersTurnDetails);

			if(GameSettings.Settings.IamGameCreator)
			{
				foreach (string playerIP in GameSettings.Settings.PlayersJoined.Keys)
				{
					// Remove the played letters from the players current letter store.
					if (GameSettings.Settings.PlayersJoined.ContainsKey(playerIP))
					{
						foreach (TileDetails tile in iPlayersTurnDetails.LettersPlayed)
						{
							GameSettings.Settings.PlayersJoined[playerIP].CurrentLetters.Letters.Remove(tile.Letter);
						}
					}

					if(playerIP != playersIpAddress)
					{
						m_localNetwork.SendPlayersTurnDetails(playerIP, iPlayersTurnDetails);
					}
					else if (iPlayersTurnDetails.LettersPlayed.Count < 7)
					{
						SendLetters(playersIpAddress, iPlayersTurnDetails.LettersPlayed.Count);
					}
				}
				GameSettings.SaveSettings();

				NextActivePlayer();
			}
		}

		void LocalNetwork_OnLettersReceived(string serverIP, GameLetters letters)
		{
			if (m_tileBeingExchanged != null)
			{
				m_panelTiles.Remove(m_tileBeingExchanged);
				m_tileBeingExchanged = null;

				if(OnChangingALetter != null)
				{
					OnChangingALetter(true);
				}
			}

			AddLettersToPanel(letters);
		}

		void LocalNetwork_OnLetterExchange(string playersIpAddress, TileDetails letter)
		{
			// Only handle the request if it is from a known player.
			if (GameSettings.Settings.PlayersJoined.ContainsKey(playersIpAddress))
			{
				GameLetters newLetter = GetLetters(1);	// Get one new letter from the bag.

				if (newLetter.Letters.Count > 0)
				{
					// Remove the returned letter from the players current letters.
					GameSettings.Settings.PlayersJoined[playersIpAddress].CurrentLetters.Letters.Remove(letter.Letter);
					// Add the new letter to the players current letters.
					GameSettings.Settings.PlayersJoined[playersIpAddress].CurrentLetters.Letters.AddRange(newLetter.Letters);
					// Add the returned latter to the letter bag.
					GameSettings.Settings.LetterBag.Add(letter.Letter);
					// Save the configuration in case we crash.
					GameSettings.SaveSettings();

					// Send the new letter to the player.
					m_localNetwork.SendLetters(playersIpAddress, newLetter);
				}
			}
		}

		public void SizeChanged()
		{
			m_tileSize = m_gameBoard.ActualWidth / GRID_SIZE;

			foreach (TileControl tile in m_playedTiles)
			{
				PlaceTileOnBoard(tile, tile.GridX, tile.GridY, ePlacementMode.Redrawing);
			}
			foreach (TileControl tile in m_currentWordTiles)
			{
				PlaceTileOnBoard(tile, tile.GridX, tile.GridY, ePlacementMode.Redrawing);
			}

			foreach (TileControl tile in m_panelTiles)
			{
				PlaceTileOnPanel(tile, tile.GridX);
			}
		}

		private void ShowMessage(string message)
		{
			if(OnShowMessage != null)
			{
				OnShowMessage(message);
			}
		}

		public void FillLetterBag()
		{
			GameSettings.Settings.LetterBag.Clear();
			foreach (UpWordsLetterConfig upWordsLetter in s_UpWordsLetters)
			{
				for (int i = 0; i < upWordsLetter.NumberOf; i++)
				{
					GameSettings.Settings.LetterBag.Add(upWordsLetter.Letter);
				}
			}

			GameSettings.SaveSettings();	// Save the configuration in case we crash.
		}

		private void NextActivePlayer()
		{
			bool activateNext = false;
			string firstIP = string.Empty;
			string playerToDeactivate = string.Empty;
			string playerToActivate = string.Empty;

			foreach (string playerIP in m_activePlayer.Keys)
			{
				if(firstIP.Length == 0)
				{
					firstIP = playerIP;
				}
				if (activateNext)
				{
					activateNext = false;
					playerToActivate = playerIP;
					break;
				}
				else if (m_activePlayer[playerIP].Active)
				{
					playerToDeactivate = playerIP;
					activateNext = true;
				}
			}

			if(playerToDeactivate.Length > 0)
			{
				m_activePlayer[playerToDeactivate].Active = false;
			}
			if (playerToActivate.Length > 0)
			{
				m_activePlayer[playerToActivate].Active = true;
				m_currentActivePlayer = m_activePlayer[playerToActivate];
			}
			else
			{
				m_activePlayer[firstIP].Active = true;
				m_currentActivePlayer = m_activePlayer[firstIP];
			}

			if (m_currentActivePlayer != null)
			{
				SendActivePlayer();
			}
		}

		private void SendActivePlayer()
		{
			foreach (string playerIP in m_activePlayer.Keys)
			{
				if (playerIP == m_localNetwork.IpAddress)
				{
					SetActivePlayer(m_currentActivePlayer);
				}
				else
				{
					GameSettings.Settings.ActivePlayer = m_currentActivePlayer.IpAddress;
					GameSettings.SaveSettings();
					m_localNetwork.SetActivePlayer(playerIP, m_currentActivePlayer);
				}
			}
		}

		private void SetActivePlayer(PlayerDetails activePlayer)
		{
			// If the player is our IP Address AND is active then it is our turn.
			if (activePlayer.IpAddress == m_localNetwork.IpAddress && activePlayer.Active)
			{
				m_turnState = eTurnState.PlayersTurn;
				if (TurnIndicator != null)
				{
					TurnIndicator("Your Turn");
				}
			}
			else
			{
				m_turnState = eTurnState.OpponentsTurn;
				if (TurnIndicator != null)
				{
					TurnIndicator("Waiting on " + activePlayer.Name);
				}
			}
		}

		private string NextRandomLetter()
		{
			int letterIndex = m_randomiser.Next(GameSettings.Settings.LetterBag.Count - 1);
			string letter = GameSettings.Settings.LetterBag[letterIndex];

			GameSettings.Settings.LetterBag.RemoveAt(letterIndex);
			
			GameSettings.SaveSettings();	// Save the configuration in case we crash.

			return letter;
		}

		private void AddTileToBoard(TileDetails tileData)
		{
			TileControl tile = new TileControl(tileData);
			tile.Visibility = Windows.UI.Xaml.Visibility.Visible;
			tile.RenderTransform = new TranslateTransform();
			tile.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Left;
			tile.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Top;
			m_tileCanvas.Children.Add(tile);

			tile.TileStatus = eTileState.JustPlayed;
			tile.ManipulationMode = ManipulationModes.None;

			m_justPlayedTiles.Add(tile);
			m_playedTiles.Add(tile);
			m_boardTiles[tile.GridX, tile.GridY] = tile;

			PlaceTileOnBoard(tile, tileData.GridX, tileData.GridY, ePlacementMode.PlacingOpponentTile);
		}

		private void AddTileToPanel(string letter, int position)
		{
			TileControl tile = new TileControl(letter);
			tile.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
			tile.RenderTransform = new TranslateTransform();
			tile.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Left;
			tile.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Top;
			m_tileCanvas.Children.Add(tile);

			tile.TileStatus = eTileState.OnPlayerPanel;
			tile.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
			tile.ManipulationStarting += DragLetter_ManipulationStarting;
			tile.ManipulationDelta += DragLetter_ManipulationDelta;
			tile.ManipulationCompleted += DragLetter_ManipulationCompleted;

			PlaceTileOnPanel(tile, position);

			m_panelTiles.Add(tile);
		}

		public GameLetters GetLetters(int numberOfLetters)
		{
			GameLetters letters = new GameLetters();

			numberOfLetters = Math.Min(numberOfLetters, GameSettings.Settings.LetterBag.Count);

			for (int i = 0; i < numberOfLetters; i++)
			{
				letters.Letters.Add(NextRandomLetter());
			}
			letters.LettersRemaining = GameSettings.Settings.LetterBag.Count;

			return letters;
		}

		public void AddLettersToPanel(GameLetters letters)
		{
			GameSettings.Settings.MyDetails.CurrentLetters.Letters.AddRange(letters.Letters);
			for (int i = 0; i < letters.Letters.Count; i++)
			{
				AddTileToPanel(letters.Letters[i], i);
			}
			
			if(LetterRemaining != null)
			{
				LetterRemaining(letters.LettersRemaining.ToString() + " letters remaining");
			}

			if(letters.Letters.Count == 0 && m_panelTiles.Count == 0)
			{
				// Signal game over.
			}
		}

		public void SetStartPlayer()
		{
			int player = 0;
			int startPlayer = m_randomiser.Next(GameSettings.Settings.PlayersJoined.Count + 1);

			foreach (string playerIP in m_activePlayer.Keys)
			{
				m_activePlayer[playerIP].Active = (player == startPlayer);

				if (player == startPlayer)
				{
					m_currentActivePlayer = m_activePlayer[playerIP];
				}
				player++;
			}
		}
		
		#region UpWords Configuration

		private class UpWordsLetterConfig
		{
			public int NumberOf;
			public string Letter;
		}

		private static List<UpWordsLetterConfig> s_UpWordsLetters = new List<UpWordsLetterConfig>()
		{
			new UpWordsLetterConfig() {NumberOf = 7, Letter = "A"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "B"},
			new UpWordsLetterConfig() {NumberOf = 4, Letter = "C"},
			new UpWordsLetterConfig() {NumberOf = 5, Letter = "D"},
			new UpWordsLetterConfig() {NumberOf = 8, Letter = "E"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "F"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "G"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "H"},
			new UpWordsLetterConfig() {NumberOf = 7, Letter = "I"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "J"},
			new UpWordsLetterConfig() {NumberOf = 2, Letter = "K"},
			new UpWordsLetterConfig() {NumberOf = 5, Letter = "L"},
			new UpWordsLetterConfig() {NumberOf = 5, Letter = "M"},
			new UpWordsLetterConfig() {NumberOf = 5, Letter = "N"},
			new UpWordsLetterConfig() {NumberOf = 7, Letter = "O"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "P"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "Qu"},
			new UpWordsLetterConfig() {NumberOf = 5, Letter = "R"},
			new UpWordsLetterConfig() {NumberOf = 6, Letter = "S"},
			new UpWordsLetterConfig() {NumberOf = 5, Letter = "T"},
			new UpWordsLetterConfig() {NumberOf = 5, Letter = "U"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "V"},
			new UpWordsLetterConfig() {NumberOf = 2, Letter = "W"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "X"},
			new UpWordsLetterConfig() {NumberOf = 2, Letter = "Y"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "Z"},
			/* Tiles for 8x8 board
			new UpWordsLetterConfig() {NumberOf = 5, Letter = "A"},
			new UpWordsLetterConfig() {NumberOf = 2, Letter = "B"},
			new UpWordsLetterConfig() {NumberOf = 2, Letter = "C"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "D"},
			new UpWordsLetterConfig() {NumberOf = 6, Letter = "E"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "F"},
			new UpWordsLetterConfig() {NumberOf = 2, Letter = "G"},
			new UpWordsLetterConfig() {NumberOf = 2, Letter = "H"},
			new UpWordsLetterConfig() {NumberOf = 4, Letter = "I"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "J"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "K"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "L"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "M"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "N"},
			new UpWordsLetterConfig() {NumberOf = 4, Letter = "O"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "P"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "Qu"},
			new UpWordsLetterConfig() {NumberOf = 2, Letter = "R"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "S"},
			new UpWordsLetterConfig() {NumberOf = 4, Letter = "T"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "U"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "V"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "W"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "X"},
			new UpWordsLetterConfig() {NumberOf = 2, Letter = "Y"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "Z"},
			*/
		};

		#endregion UpWords Configuration

		#region Letter Dragging Functionality

		void DragLetter_ManipulationStarting(object sender, ManipulationStartingRoutedEventArgs e)
		{
			if (m_turnState != eTurnState.PlayersTurn)
			{
				e.Handled = true;
			}
			else if (ChangingALetter)
			{
				ExchangeTile(sender as TileControl);
				e.Handled = true;
			}
		}

		private void DragLetter_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
		{
			if (!e.IsInertial)
			{
				TileControl dragableItem = sender as TileControl;
				TranslateTransform tileRenderTransform = dragableItem.RenderTransform as TranslateTransform;

				tileRenderTransform.X += e.Delta.Translation.X;
				if (m_turnState == eTurnState.PlayersTurn)
				{
					tileRenderTransform.Y += e.Delta.Translation.Y;
				}

				Canvas.SetZIndex(dragableItem, 100);
			}
			else
			{
				e.Complete();
			}
		}

		void DragLetter_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
		{
			try
			{
				TileControl draggedItem = (TileControl)sender;

				GeneralTransform imageTrasform = draggedItem.TransformToVisual(m_tileCanvas);
				Point imagePosition = imageTrasform.TransformPoint(new Point());
				Point cursorPosition = imageTrasform.TransformPoint(e.Position);
				Rect imageRect = imageTrasform.TransformBounds(new Rect(0, 0, draggedItem.ActualWidth, draggedItem.ActualHeight));

				GeneralTransform boardTransform = m_gameBoard.TransformToVisual(m_tileCanvas);
				Rect boardRect = boardTransform.TransformBounds(new Rect(0, 0, m_gameBoard.ActualWidth, m_gameBoard.ActualHeight));
				GeneralTransform tilePanelTransform = m_tilePanel.TransformToVisual(m_tileCanvas);

				double startX = (m_tilePanel.ActualWidth / 2) - (m_tileSize * 7) / 2;
				double endX = (m_tilePanel.ActualWidth / 2) + (m_tileSize * 7) / 2;
				Rect letterPanelRect = tilePanelTransform.TransformBounds(new Rect(startX, 0, endX, m_tilePanel.ActualHeight));

				bool droppedOnBoard = boardRect.Contains(cursorPosition) || boardRect.Contains(imagePosition);
				bool droppedOnLetterPanel = letterPanelRect.Contains(cursorPosition) || letterPanelRect.Contains(imagePosition);

				if (droppedOnLetterPanel)
				{
					int gridX = 0;
					double desiredX = 0;
					double desiredY = 0;
					double placementX = 0;

					if (!e.IsInertial)
					{
						placementX = cursorPosition.X;
					}
					else
					{
						placementX = imagePosition.X + (m_tileSize / 2);
					}

					desiredX = placementX - ((placementX - letterPanelRect.X) % m_tileSize);
					desiredY = letterPanelRect.Y;
					gridX = Math.Max(0, Math.Min(6, (int)((1 + desiredX - letterPanelRect.X) / m_tileSize)));

					PlaceTileOnPanel(draggedItem, gridX);
				}
				else if (droppedOnBoard)
				{
					int gridX = 0;
					int gridY = 0;
					double desiredX = 0;
					double desiredY = 0;
					double placementX = 0;
					double placementY = 0;
					TranslateTransform tileRenderTransform = draggedItem.RenderTransform as TranslateTransform;

					if (!e.IsInertial)
					{
						placementX = cursorPosition.X;
						placementY = cursorPosition.Y;
					}
					else
					{
						placementX = imagePosition.X + (m_tileSize / 2);
						placementY = imagePosition.Y + (m_tileSize / 2);
					}

					desiredX = placementX - ((placementX - boardRect.X) % m_tileSize);
					desiredY = placementY - ((placementY - boardRect.Y) % m_tileSize);
					gridX = Math.Max(0, Math.Min(GRID_SIZE - 1, (int)((1 + desiredX - boardRect.X) / m_tileSize)));
					gridY = Math.Max(0, Math.Min(GRID_SIZE - 1, (int)((1 + desiredY - boardRect.Y) / m_tileSize)));

					foreach (TileControl tile in m_currentWordTiles)
					{
						if (tile != draggedItem && tile.GridX == gridX && tile.GridY == gridY)
						{
							FindNearestBoardSpace(placementX - boardRect.X, placementY - boardRect.Y, ref gridX, ref gridY);
							break;
						}
					}
					foreach (TileControl tile in m_playedTiles)
					{
						if (tile != draggedItem && m_boardSpaceFilled[gridX, gridY] == MAX_TILE_ON_SPACE)
						{
							FindNearestBoardSpace(placementX - boardRect.X, placementY - boardRect.Y, ref gridX, ref gridY);
							break;
						}
					}

					PlaceTileOnBoard(draggedItem, gridX, gridY, ePlacementMode.UserPlacement);
				}
				else
				{
					PlaceTileOnPanel(draggedItem, 0);
				}

				TileMoved();
			}
			catch /*(Exception ex)*/
			{
				//int x = 0;
			}
		}

		private void FindNearestBoardSpace(double placementX, double placementY, ref int gridX, ref int gridY)
		{
			bool left = (placementX % m_tileSize) < (m_tileSize / 2);
			bool up = (placementY % m_tileSize) < (m_tileSize / 2);

			// Move up one if possible.
			if (up && gridY > 0 && m_boardSpaceFilled[gridX, gridY - 1] == 0)
			{
				gridY--;
			}
			// Move down one if possible.
			else if (!up && gridY + 1 < GRID_SIZE && m_boardSpaceFilled[gridX, gridY + 1] == 0)
			{
				gridY++;
			}
			// Move left one if possible.
			else if (left && gridX > 0 && m_boardSpaceFilled[gridX - 1, gridY] == 0)
			{
				gridX--;
			}
			// Move right one if possible.
			else if (!left && gridX - 1 < GRID_SIZE && m_boardSpaceFilled[gridX + 1, gridY] == 0)
			{
				gridX++;
			}
			// Move up and left if possible.
			else if (left && up && gridX > 0 && gridY > 0 && m_boardSpaceFilled[gridX - 1, gridY - 1] == 0)
			{
				gridX--;
				gridY--;
			}
			// Move down and left if possible.
			else if (left && !up && gridX > 0 && gridY + 1 < GRID_SIZE && m_boardSpaceFilled[gridX - 1, gridY + 1] == 0)
			{
				gridX--;
				gridY++;
			}
			// Move up and right if possible.
			else if (!left && up && gridX + 1 < GRID_SIZE && gridY > 0 && m_boardSpaceFilled[gridX + 1, gridY - 1] == 0)
			{
				gridX++;
				gridY--;
			}
			// Move down and right if possible.
			else if (!left && !up && gridX + 1 < GRID_SIZE && gridY + 1 < GRID_SIZE && m_boardSpaceFilled[gridX + 1, gridY + 1] == 0)
			{
				gridX++;
				gridY++;
			}
			// If no single space moves are possible then try to find any free space.
			else
			{
				bool spaceFound = false;
				if (up && gridY > 1)
				{
					for (int row = gridY; row >= 0; row--)
					{
						if (m_boardSpaceFilled[gridX, row] == 0)
						{
							spaceFound = true;
							gridY = row;
							break;
						}
					}
				}
				if (!spaceFound && !up && gridY + 1 < GRID_SIZE)
				{
					for (int row = gridY; row + 1 < GRID_SIZE; row++)
					{
						if (m_boardSpaceFilled[gridX, row] == 0)
						{
							spaceFound = true;
							gridY = row;
							break;
						}
					}
				}
				if (!spaceFound && left && gridX > 1)
				{
					for (int column = gridX; column >= 0; column--)
					{
						if (m_boardSpaceFilled[column, gridY] == 0)
						{
							spaceFound = true;
							gridX = column;
							break;
						}
					}
				}
				if (!spaceFound && !left && gridX + 1 < GRID_SIZE)
				{
					for (int column = gridX; column + 1 < GRID_SIZE; column++)
					{
						if (m_boardSpaceFilled[column, gridY] == 0)
						{
							spaceFound = true;
							gridX = column;
							break;
						}
					}
				}
			}
		}

		private void TileMoved()
		{
			/*
			if (m_panelTiles.Count == 7)
			{
				PlayButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				RecallLettersButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				PassButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
				SwapLettersButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
			}
			else
			{
				PlayButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
				RecallLettersButton.Visibility = Windows.UI.Xaml.Visibility.Visible;
				PassButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
				SwapLettersButton.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
			}
			*/
		}

		private void PlaceTileOnPanel(TileControl draggedItem, int gridX)
		{
			TranslateTransform tileRenderTransform = draggedItem.RenderTransform as TranslateTransform;

			draggedItem.Layer = -1;
			Canvas.SetZIndex(draggedItem, 1);

			foreach (TileControl tile in m_panelTiles)
			{
				// If a tile is dropped on an existing tile...
				if (tile != draggedItem && tile.GridX == gridX)
				{
					// If moving tiles that are already on the panel then shuffle up or down as appropriate
					if (draggedItem.GridY == ON_PANEL)
					{
						// If the tile is being moved right then shuffle all preceding tiles right
						if (draggedItem.GridX < tile.GridX)
						{
							foreach (TileControl tileToMove in m_panelTiles)
							{
								if (tileToMove.GridX > draggedItem.GridX && tileToMove.GridX <= tile.GridX)
								{
									tileToMove.GridX--;
									TranslateTransform movingTileRenderTransform = tileToMove.RenderTransform as TranslateTransform;
									movingTileRenderTransform.X -= m_tileSize;
								}
							}
						}
						// If the tile is being moved left then shuffle all preceding tiles right
						else if (draggedItem.GridX > tile.GridX)
						{
							foreach (TileControl tileToMove in m_panelTiles)
							{
								if (tileToMove.GridX < draggedItem.GridX && tileToMove.GridX >= tile.GridX)
								{
									tileToMove.GridX++;
									TranslateTransform movingTileRenderTransform = tileToMove.RenderTransform as TranslateTransform;
									movingTileRenderTransform.X += m_tileSize;
								}
							}
						}
					}
					else
					{
						int spacesToTheLeft = gridX;
						int spacesToTheRight = 6 - gridX;
						int gapToLeftAt = 0;
						int gapToRightAt = 6;
						bool[] existingTiles = new bool[7];

						foreach (TileControl tileToMove in m_panelTiles)
						{
							existingTiles[tileToMove.GridX] = true;
							if (tileToMove.GridX < gridX)
							{
								spacesToTheLeft--;
							}
							else if (tileToMove.GridX > gridX)
							{
								spacesToTheRight--;
							}
						}
						for (int i = gridX; i >= 0; i--)
						{
							if (!existingTiles[i])
							{
								gapToLeftAt = i;
								break;
							}
						}
						for (int i = gridX; i < 7; i++)
						{
							if (!existingTiles[i])
							{
								gapToRightAt = i;
								break;
							}
						}
						// If there is no space on the left or simple more space on the right then shuffle right.
						if (spacesToTheLeft == 0 || spacesToTheRight >= spacesToTheLeft)
						{
							foreach (TileControl tileToMove in m_panelTiles)
							{
								if (tileToMove.GridX >= gridX && tileToMove.GridX < gapToRightAt)
								{
									tileToMove.GridX++;
									TranslateTransform movingTileRenderTransform = tileToMove.RenderTransform as TranslateTransform;
									movingTileRenderTransform.X += m_tileSize;
								}
							}
						}
						// Otherwise shuffle left.
						else if (spacesToTheRight == 0 || spacesToTheRight < spacesToTheLeft)
						{
							foreach (TileControl tileToMove in m_panelTiles)
							{
								if (tileToMove.GridX <= gridX && tileToMove.GridX > gapToLeftAt)
								{
									tileToMove.GridX--;
									TranslateTransform movingTileRenderTransform = tileToMove.RenderTransform as TranslateTransform;
									movingTileRenderTransform.X -= m_tileSize;
								}
							}
						}
					}
					break;
				}
			}

			GeneralTransform tilePanelTransform = m_tilePanel.TransformToVisual(m_tileCanvas);
			Point positionOfBoard = tilePanelTransform.TransformPoint(new Point(0, 0));

			tileRenderTransform.X = positionOfBoard.X + m_tilePanel.ActualWidth / 2 - (m_tileSize * (3.5 - gridX));
			tileRenderTransform.Y = positionOfBoard.Y + 1;

			if (draggedItem.GridY != ON_PANEL && draggedItem.GridX >= 0 && draggedItem.GridY >= 0)
			{
				m_boardSpaceFilled[draggedItem.GridX, draggedItem.GridY]--;
			}

			draggedItem.Width = m_tileSize;
			draggedItem.Height = m_tileSize;
			draggedItem.GridX = gridX;
			draggedItem.GridY = ON_PANEL;
			draggedItem.Visibility = Windows.UI.Xaml.Visibility.Visible;

			if (draggedItem.TileStatus == eTileState.ComposingNewWord)
			{
				draggedItem.TileStatus = eTileState.OnPlayerPanel;
				m_currentWordTiles.Remove(draggedItem);
				m_panelTiles.Add(draggedItem);
			}
		}


		private void PlaceTileOnBoard(TileControl draggedItem, int gridX, int gridY, ePlacementMode placement)
		{
			TranslateTransform tileRenderTransform = draggedItem.RenderTransform as TranslateTransform;

			GeneralTransform boardTransform = m_gameBoard.TransformToVisual(m_tileCanvas);
			Rect boardRect = boardTransform.TransformBounds(new Rect(0, 0, m_gameBoard.ActualWidth, m_gameBoard.ActualHeight));
			Point positionOfBoard = boardTransform.TransformPoint(new Point(0, 0));

			if (placement == ePlacementMode.UserPlacement)
			{
				// If the tile is being moved around the board then decrement the tile count in the square it left.
				if (draggedItem.GridY != ON_PANEL && draggedItem.GridX >= 0 && draggedItem.GridY >= 0)
				{
					m_boardSpaceFilled[draggedItem.GridX, draggedItem.GridY]--;
				}
				m_boardSpaceFilled[gridX, gridY] = Math.Max(1, Math.Min(5, m_boardSpaceFilled[gridX, gridY] + 1));
				draggedItem.Layer = m_boardSpaceFilled[gridX, gridY];
			}
			else if(placement == ePlacementMode.PlacingOpponentTile)
			{
				m_boardSpaceFilled[gridX, gridY] = draggedItem.Layer;
			}

			draggedItem.Width = m_tileSize;
			draggedItem.Height = m_tileSize;
			draggedItem.GridX = gridX;
			draggedItem.GridY = gridY;
			draggedItem.Visibility = Windows.UI.Xaml.Visibility.Visible;

			tileRenderTransform.X = positionOfBoard.X + (m_tileSize * gridX) - (boardRect.Width * 0.005 * draggedItem.Layer);// +boardRect.Width * 0.01 - (boardRect.Width * 0.005 * draggedItem.Layer);
			tileRenderTransform.Y = positionOfBoard.Y + (m_tileSize * gridY) - (boardRect.Height * 0.005 * draggedItem.Layer);// +boardRect.Height * 0.01 - (boardRect.Height * 0.005 * draggedItem.Layer);
			Canvas.SetZIndex(draggedItem, draggedItem.Layer);

			if (draggedItem.TileStatus == eTileState.OnPlayerPanel)
			{
				draggedItem.TileStatus = eTileState.ComposingNewWord;
				m_panelTiles.Remove(draggedItem);
				m_currentWordTiles.Add(draggedItem);
			}

			PlaySound(draggedItem.Layer);
		}

		#endregion Letter Dragging Functionality

		private async void PlaySound(int level)
		{
			MediaElement soundElement = new MediaElement();
			StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(string.Format("ms-appx:///Assets/Level{0}.mp3", level), UriKind.Absolute)); 
			var stream = await file.OpenAsync(FileAccessMode.Read);
			soundElement.SetSource(stream, file.ContentType);
			soundElement.Play();
		}

		#region Server interactions.

		private void ExchangeTile(TileControl tileToExchange)
		{
			m_tileBeingExchanged = tileToExchange;

			// Request tile exchange.
			m_localNetwork.ExchangeLetter(tileToExchange.TileData);
		}

		#endregion Server interactions.

		#region Word finding functionality.

		private List<UpWord> GetPlayedWords()
		{
			bool horizontal = true;
			UpWord playedWord = null;
			List<UpWord> playedWords = new List<UpWord>();
			List<TileControl> playedTiles = new List<TileControl>();

			horizontal = SortTiles(m_currentWordTiles, ref playedTiles);

			if (playedTiles == null)
			{
				return playedWords;
			}

			if (horizontal)
			{
				playedWord = GetMainWordPlayedHorizontal(playedTiles);
			}
			else
			{
				playedWord = GetMainWordPlayedVertical(playedTiles);
			}

			if (playedWord != null)
			{
				if (playedWord.Word.Length > 1 && playedWord.Word != "QU")
				{
					playedWords.Add(playedWord);
				}
				if (horizontal)
				{
					GetSideWordsForWordPlayedHorizontal(playedTiles, ref playedWords);
				}
				else
				{
					GetSideWordsForWordPlayedVertical(playedTiles, ref playedWords);
				}
			}

			return playedWords;
		}

		private bool SortTiles(List<TileControl> sourceList, ref List<TileControl> destinationList)
		{
			int sortIndex = 0;
			bool horizontal = true;
			TileControl firstTile = sourceList[0];

			destinationList.Add(firstTile);

			// Sort the letters into the order in which they appear on the board.
			for (int index = 1; index < sourceList.Count; index++)
			{
				if (firstTile.GridX == sourceList[index].GridX)
				{
					horizontal = false;
					for (sortIndex = 0; sortIndex < destinationList.Count; sortIndex++)
					{
						if (destinationList[sortIndex].GridY > sourceList[index].GridY)
						{
							break;
						}
					}
					destinationList.Insert(sortIndex, sourceList[index]);
				}
				else
				{
					horizontal = true;
					for (sortIndex = 0; sortIndex < destinationList.Count; sortIndex++)
					{
						if (destinationList[sortIndex].GridX > sourceList[index].GridX)
						{
							break;
						}
					}
					destinationList.Insert(sortIndex, sourceList[index]);
				}
			}

			return horizontal;
		}

		private UpWord GetMainWordPlayedHorizontal(List<TileControl> playedTiles)
		{
			int index;
			int lastX = -1;
			UpWord wordCreated = new UpWord();

			// Find the word that has been played.
			wordCreated.AppendLetter(playedTiles[0]);			

			// Prepend any letters that appear before the played word.
			if (playedTiles[0].GridX > 0)
			{
				int previousX = playedTiles[0].GridX - 1;
				while (previousX >= 0 && m_boardTiles[previousX, playedTiles[0].GridY] != null)
				{
					wordCreated.PrependLetter(m_boardTiles[previousX, playedTiles[0].GridY]);
					previousX--;
				}
			}

			// Now fill in the played letters and any that are already on the board.
			lastX = playedTiles[0].GridX;
			for (index = 1; index < playedTiles.Count; index++)
			{
				// Fill in any letters that are already on the board.
				if (lastX != playedTiles[index].GridX - 1)
				{
					for (int X = lastX + 1; X < playedTiles[index].GridX; X++)
					{
						if (m_boardTiles[X, playedTiles[index].GridY] != null)
						{
							wordCreated.AppendLetter(m_boardTiles[X, playedTiles[index].GridY]);
						}
						else
						{
							ShowMessage("There seems to be a gap in your word!");
							wordCreated = null; ;
							return null;
						}
					}
				}

				// Add the current tile.
				wordCreated.AppendLetter(playedTiles[index]);

				// Save the last X value;
				lastX = playedTiles[index].GridX;
			}

			// Append any letters that appear after the played word.
			if (lastX < GRID_SIZE)
			{
				int nextX = lastX + 1;
				while (nextX < GRID_SIZE && m_boardTiles[nextX, playedTiles[0].GridY] != null)
				{
					wordCreated.AppendLetter(m_boardTiles[nextX, playedTiles[0].GridY]);
					nextX++;
				}
			}

			return wordCreated;
		}

		private UpWord GetMainWordPlayedVertical(List<TileControl> playedTiles)
		{
			int index;
			int lastY = -1;
			UpWord wordCreated = new UpWord();

			// Find the word that has been played.
			wordCreated.AppendLetter(playedTiles[0]);

			// Prepend any letters that appear before the played word.
			if (playedTiles[0].GridY > 0)
			{
				int previousY = playedTiles[0].GridY - 1;
				while (previousY >= 0 && m_boardTiles[playedTiles[0].GridX, previousY] != null)
				{
					wordCreated.PrependLetter(m_boardTiles[playedTiles[0].GridX, previousY]);
					previousY--;
				}
			}

			// Now fill in the played letters and any that are already on the board.
			lastY = playedTiles[0].GridY;
			for (index = 1; index < playedTiles.Count; index++)
			{
				// Fill in any letters that are already on the board.
				if (lastY != playedTiles[index].GridY - 1)
				{
					for (int Y = lastY + 1; Y < playedTiles[index].GridY; Y++)
					{
						if (m_boardTiles[playedTiles[index].GridX, Y] != null)
						{
							wordCreated.AppendLetter(m_boardTiles[playedTiles[index].GridX, Y]);
						}
						else
						{
							ShowMessage("There seems to be a gap in your word!");
							wordCreated = null;
							return null;
						}
					}
				}
				// Add the current tile.
				wordCreated.AppendLetter(playedTiles[index]);

				// Save the last Y value;
				lastY = playedTiles[index].GridY;
			}

			// Append any letters that appear after the played word.
			if (lastY < GRID_SIZE)
			{
				int nextY = lastY + 1;
				while (nextY < GRID_SIZE && m_boardTiles[playedTiles[0].GridX, nextY] != null)
				{
					wordCreated.AppendLetter(m_boardTiles[playedTiles[0].GridX, nextY]);
					nextY++;
				}
			}

			return wordCreated;
		}

		private void GetSideWordsForWordPlayedHorizontal(List<TileControl> playedTiles, ref List<UpWord> playedWords)
		{
			foreach (TileControl tile in playedTiles)
			{
				// If there are letters on the board above the played tile then add those first.
				if (tile.GridY > 0 && m_boardTiles[tile.GridX, tile.GridY - 1] != null)
				{
					int Y = tile.GridY - 1;
					UpWord wordCreated = new UpWord();

					// Work up the column until an empty space is found.
					while (Y > 0 && m_boardTiles[tile.GridX, Y - 1] != null)
					{
						Y--;
					}
					// Now work back down to build up the word.
					while (Y < GRID_SIZE && (m_boardTiles[tile.GridX, Y] != null || Y == tile.GridY))
					{
						if (Y == tile.GridY)
						{
							wordCreated.AppendLetter(tile);
						}
						else
						{
							wordCreated.AppendLetter(m_boardTiles[tile.GridX, Y]);
						}
						Y++;
					}

					if (wordCreated.Word.Length > 1 && wordCreated.Word != "QU")
					{
						playedWords.Add(wordCreated);
					}
				}
				// If there are letters on the board below the played tile then start with the played tile and go from there.
				else if (tile.GridY + 1 < GRID_SIZE && m_boardTiles[tile.GridX, tile.GridY + 1] != null)
				{
					int Y = tile.GridY + 1;
					UpWord wordCreated = new UpWord();

					wordCreated.AppendLetter(tile);

					// Now work back down to build up the word.
					while (Y < GRID_SIZE && m_boardTiles[tile.GridX, Y] != null)
					{
						wordCreated.AppendLetter(m_boardTiles[tile.GridX, Y]);
						Y++;
					}

					if (wordCreated.Word.Length > 1 && wordCreated.Word != "QU")
					{
						playedWords.Add(wordCreated);
					}
				}
			}
		}

		private void GetSideWordsForWordPlayedVertical(List<TileControl> playedTiles, ref List<UpWord> playedWords)
		{
			foreach (TileControl tile in playedTiles)
			{
				// If there are letters on the board to the left of the played tile then add those first.
				if (tile.GridX > 0 && m_boardTiles[tile.GridX - 1, tile.GridY] != null)
				{
					int X = tile.GridX - 1;
					UpWord wordCreated = new UpWord();

					// Work back along the row until an empty space is found.
					while (X > 0 && m_boardTiles[X - 1, tile.GridY] != null)
					{
						X--;
					}
					// Now work forward to build up the word.
					while (X < GRID_SIZE && (m_boardTiles[X, tile.GridY] != null || X == tile.GridX))
					{
						if (X == tile.GridX)
						{
							wordCreated.AppendLetter(tile);
						}
						else
						{
							wordCreated.AppendLetter(m_boardTiles[X, tile.GridY]);
						}
						X++;
					}

					if (wordCreated.Word.Length > 1 && wordCreated.Word != "QU")
					{
						playedWords.Add(wordCreated);
					}
				}
				// If there are letters on the board to the right of the played tile then start with the played tile and go from there.
				else if (tile.GridX + 1 < GRID_SIZE && m_boardTiles[tile.GridX + 1, tile.GridY] != null)
				{
					int X = tile.GridX + 1;
					UpWord wordCreated = new UpWord();

					wordCreated.AppendLetter(tile);

					// Now work forward to build up the word.
					while (X < GRID_SIZE && m_boardTiles[X, tile.GridY] != null)
					{
						wordCreated.AppendLetter(m_boardTiles[X, tile.GridY]);
						X++;
					}

					if (wordCreated.Word.Length > 1 && wordCreated.Word != "QU")
					{
						playedWords.Add(wordCreated);
					}
				}
			}
		}

		#endregion Word finding functionality.

		private bool TilesAreLayedCorrectly()
		{
			if (m_currentWordTiles.Count == 0)
			{
				ShowMessage("You have not placed any letters!");
				return false;
			}
			else if (m_firstWord)
			{
				bool goesThroughCenter = false;
				foreach (TileControl tile in m_currentWordTiles)
				{
					if (tile.GridX > 3 && tile.GridX < 6 && tile.GridY > 3 && tile.GridY < 6)
					{
						goesThroughCenter = true;
						break;
					}
				}
				if (!goesThroughCenter)
				{
					ShowMessage("The first word must go through the center four squares!");
					return false;
				}
			}
			else
			{
				int firstTileX = m_currentWordTiles[0].GridX;
				int firstTileY = m_currentWordTiles[0].GridY;
				bool allTilesHorizontal = true;
				bool allTilesVertical = true;
				bool coversIdenticalLetter = false;
				bool touchesExistingWord = false;
				string identicalLetter = string.Empty;
				List<TileControl> coveredTiles = new List<TileControl>();
				List<TileControl> sortedCoveredTiles = new List<TileControl>();

				foreach (TileControl tile in m_currentWordTiles)
				{
					if(tile.GridX != firstTileX)
					{
						allTilesVertical = false;
					}
					if (tile.GridY != firstTileY)
					{
						allTilesHorizontal = false;
					}
					// Gather a list of all covered tiles to check if an entire word has been covered.
					if (m_boardTiles[tile.GridX, tile.GridY] != null)									// Space contains a letter.
					{
						coveredTiles.Add(m_boardTiles[tile.GridX, tile.GridY]);
					}
					// Check that the played word interacts with the existing played words.
					if (m_boardTiles[tile.GridX, tile.GridY] != null ||									// Space contains a letter.
						m_boardTiles[Math.Max(0, tile.GridX - 1), tile.GridY] != null ||				// Letter to the left
						m_boardTiles[Math.Min(GRID_SIZE - 1, tile.GridX + 1), tile.GridY] != null ||	// Letter to the right
						m_boardTiles[tile.GridX, Math.Max(0, tile.GridY - 1)] != null ||				// Letter above
						m_boardTiles[tile.GridX, Math.Min(GRID_SIZE - 1, tile.GridY + 1)] != null)		// Letter below
					{
						touchesExistingWord = true;
					}
					// Check that the letter covered is not the same as the letter placed.
					if (m_boardTiles[tile.GridX, tile.GridY] != null && 
						m_boardTiles[tile.GridX, tile.GridY].Letter == tile.Letter)
					{
						coversIdenticalLetter = true;
						identicalLetter = tile.Letter;
					}
				}

				if (!allTilesHorizontal && allTilesVertical)
				{
					ShowMessage("The tiles are not in a line!");
					return false;
				}
				else if (!touchesExistingWord)
				{
					ShowMessage("Your word must interact with an existing word.");
					return false;
				}
				else if (coversIdenticalLetter)
				{
					ShowMessage("Your '" + identicalLetter + "' covers an existing '" + identicalLetter + "'.");
					return false;
				}

				if (coveredTiles.Count > 1)
				{
					bool horizontal = SortTiles(coveredTiles, ref sortedCoveredTiles);
					bool entireWordCovered = true;
					TileControl firstTile = sortedCoveredTiles[0];
					TileControl lastTile = sortedCoveredTiles[sortedCoveredTiles.Count - 1];

					// Check for entire word coverage horizontally.
					if (horizontal)
					{
						// If there is a letter before the first covered letter then the entire word is not covered.
						if (firstTile.GridX > 0 && m_boardTiles[firstTile.GridX - 1, firstTile.GridY] != null)
						{
							entireWordCovered = false;
						}
						// If there is a letter after the last covered letter then the entire word is not covered.
						else if (lastTile.GridX + 1 < GRID_SIZE  && m_boardTiles[lastTile.GridX + 1, lastTile.GridY] != null)
						{
							entireWordCovered = false;
						}
						// Otherwise check all letters in between
						else
						{
							for (int index = 1; index < sortedCoveredTiles.Count; index++)
							{
								// If there is a gap in the placed letters then an entire word is not covered.
								if (sortedCoveredTiles[index].GridX - sortedCoveredTiles[index-1].GridX > 1)
								{
									entireWordCovered = false;
									break;
								}
								// If the space does not contain a letter then an entire word is not covered.
								else if (m_boardTiles[sortedCoveredTiles[index].GridX, sortedCoveredTiles[index].GridY] == null)
								{
									entireWordCovered = false;
									break;
								}
							}
						}
					}
					// Check for entire work coverage vertically.
					else
					{
						// If there is a letter above the first covered letter then the entire word is not covered.
						if (firstTile.GridY > 0 && m_boardTiles[firstTile.GridX, firstTile.GridY - 1] != null)
						{
							entireWordCovered = false;
						}
						// If there is a letter below the last covered letter then the entire word is not covered.
						else if (lastTile.GridY + 1 < GRID_SIZE && m_boardTiles[lastTile.GridX, lastTile.GridY + 1] != null)
						{
							entireWordCovered = false;
						}
						// Otherwise check all letters in between
						else
						{
							for (int index = 1; index < sortedCoveredTiles.Count; index++)
							{
								// If there is a gap in the placed letters then an entire word is not covered.
								if (sortedCoveredTiles[index].GridY - sortedCoveredTiles[index - 1].GridY > 1)
								{
									entireWordCovered = false;
									break;
								}
								// If the space does not contain a letter then an entire word is not covered.
								else if (m_boardTiles[sortedCoveredTiles[index].GridX, sortedCoveredTiles[index].GridY] == null)
								{
									entireWordCovered = false;
									break;
								}
							}
						}						
					}

					if (entireWordCovered)
					{
						string wordCovered = string.Empty;
						foreach (TileControl tile in sortedCoveredTiles)
						{
							wordCovered += tile.Letter;
						}
						ShowMessage("Your word completely covers the existing word '" + wordCovered + "'.");
						return false;
					}
				}
			}
			return true;
		}

		public void SubmitWords()
		{
			if (TilesAreLayedCorrectly())
			{
				bool allWordsOk = true;
				List<UpWord> playedWords = GetPlayedWords();

				if (playedWords.Count == 0)
				{
					return;
				}

				foreach (UpWord playedWord in playedWords)
				{
					if (playedWord.Word.Length > 0)
					{
						if (!m_words.Contains(playedWord.Word))
						{
							ShowMessage("My dictionary does not contain the word '" + playedWord.Word + "'.");
							allWordsOk = false;
							break;
						}
					}
				}

				if (allWordsOk)
				{
					int replacementTiles = m_currentWordTiles.Count;
					PlayersTurnDetails iPlayersTurnDetails = new PlayersTurnDetails();
					iPlayersTurnDetails.PlayersIP = m_localNetwork.IpAddress;
					iPlayersTurnDetails.PlayerName = m_localNetwork.Username;
					iPlayersTurnDetails.PlayedWords = playedWords;

					foreach (UpWord playedWord in iPlayersTurnDetails.PlayedWords)
					{
						iPlayersTurnDetails.Score += playedWord.Score;
					}
					GameSettings.Settings.TotalScore += iPlayersTurnDetails.Score;
					GameSettings.SaveSettings();

					iPlayersTurnDetails.TotalScore += GameSettings.Settings.TotalScore;

					foreach (TileControl tile in m_justPlayedTiles)
					{
						tile.TileStatus = eTileState.Played;
					}

					m_justPlayedTiles.Clear();

					foreach (TileControl tile in m_currentWordTiles)
					{
						tile.TileStatus = eTileState.JustPlayed;
						tile.ManipulationMode = ManipulationModes.None;
						m_justPlayedTiles.Add(tile);
						m_playedTiles.Add(tile);
						m_boardTiles[tile.GridX, tile.GridY] = tile;

						iPlayersTurnDetails.LettersPlayed.Add(tile.TileData);
					}

					m_firstWord = false;
					m_currentWordTiles.Clear();

					DisplayPlay(iPlayersTurnDetails);

					if (GameSettings.Settings.IamGameCreator)
					{
						foreach (string playerIP in GameSettings.Settings.PlayersJoined.Keys)
						{
							m_localNetwork.SendPlayersTurnDetails(playerIP, iPlayersTurnDetails);
						}

						AddLettersToPanel(GetLetters(replacementTiles));
						NextActivePlayer();
					}
					else
					{
						m_localNetwork.SendPlayersTurnDetails(GameSettings.Settings.CreatorsIpAddress, iPlayersTurnDetails);
					}
				}
			}
		}

		private void DisplayPlay(PlayersTurnDetails iPlayersTurnDetails)
		{
			if (iPlayersTurnDetails.PlayersIP != m_localNetwork.IpAddress)
			{
				foreach (TileControl tile in m_justPlayedTiles)
				{
					tile.TileStatus = eTileState.Played;
				}

				m_justPlayedTiles.Clear();

				foreach (TileDetails tileData in iPlayersTurnDetails.LettersPlayed)
				{
					AddTileToBoard(tileData);
				}

				m_firstWord = false;
			}

			if (OnDisplayPlays != null)
			{
				string message = string.Empty;

				foreach (UpWord playedWord in iPlayersTurnDetails.PlayedWords)
				{
					if (message.Length > 0)
					{
						message += ", ";
					}
					message += playedWord.Word;
				}
				message = "Scored " + iPlayersTurnDetails.Score.ToString() + 
						  " points from:" + (iPlayersTurnDetails.PlayedWords.Count > 1 ? "s " : " ") + message +
						  ".\r\n";

				Paragraph detail = new Windows.UI.Xaml.Documents.Paragraph();
				detail.Inlines.Add(new Windows.UI.Xaml.Documents.Run() { FontSize = 20, Text = message });

				Paragraph title = new Windows.UI.Xaml.Documents.Paragraph();
				title.Inlines.Add(new Windows.UI.Xaml.Documents.Run()
				{
					FontSize = 24,
					Text = iPlayersTurnDetails.PlayerName + " - Score: " + iPlayersTurnDetails.TotalScore.ToString()
				});

				m_currentGame.PlayDetails.Insert(0, detail);
				m_currentGame.PlayDetails.Insert(0, title);

				OnDisplayPlays(title, detail);
			}
		}

		public void ShuffleLetters()
		{
			List<int> positions = new List<int>() { 0, 1, 2, 3, 4, 5, 6 };
			GeneralTransform boardTransform = m_gameBoard.TransformToVisual(m_tileCanvas);
			Point positionOfBoard = boardTransform.TransformPoint(new Point(0, 0));

			foreach (TileControl tile in m_panelTiles)
			{
				int selector = m_randomiser.Next(positions.Count);

				PlaceTileOnPanel(tile, positions[selector]);

				positions.RemoveAt(selector);
			}
		}

		public void RecallLetters()
		{
			while (m_currentWordTiles.Count > 0)
			{
				PlaceTileOnPanel(m_currentWordTiles[0], m_panelTiles.Count - 1);
			}
		}

		public async Task<IRandomAccessStream> BeepBeep(int Amplitude, int Frequency, int Duration)
		{
			double A = ((Amplitude * (System.Math.Pow(2, 15))) / 1000) - 1;
			double DeltaFT = 2 * Math.PI * Frequency / 44100.0;

			int Samples = 441 * Duration / 10;
			int Bytes = Samples * 4;
			int[] Hdr = { 0X46464952, 36 + Bytes, 0X45564157, 0X20746D66, 16, 0X20001, 44100, 176400, 0X100004, 0X61746164, Bytes };

			InMemoryRandomAccessStream ims = new InMemoryRandomAccessStream();
			IOutputStream outStream = ims.GetOutputStreamAt(0);
			DataWriter dw = new DataWriter(outStream);
			dw.ByteOrder = ByteOrder.LittleEndian;

			for (int I = 0; I < Hdr.Length; I++)
			{
				dw.WriteInt32(Hdr[I]);
			}
			for (int T = 0; T < Samples; T++)
			{
				short Sample = System.Convert.ToInt16(A * Math.Sin(DeltaFT * T));
				dw.WriteInt16(Sample);
				dw.WriteInt16(Sample);
			}
			await dw.StoreAsync();
			await outStream.FlushAsync();
			return ims;
		}

		/*
		private async void Button_Click()
		{			
			IRandomAccessStream beepStream = await BeepBeep(200, 3000, 250);
			Beeper.SetSource(beepStream, string.Empty);
			Beeper.Play();
		}
		*/
	}
}
