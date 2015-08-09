using System;
using System.Collections.Generic;
using System.Text;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace UpWords
{
    public class UpwordsGame
    {
		public delegate void ShowMessageHandler(string message);
		public event ShowMessageHandler OnShowMessage;
		public event ShowMessageHandler TurnIndicator;

//		public delegate void HideMessageHandler();
//		public event HideMessageHandler OnHideMessage;

		public delegate void LetterChangingHandler(bool changingALetter);
		public event LetterChangingHandler OnChangingALetter;

		public delegate void ShowPlaysHandler(Paragraph title, Paragraph detail);
		public event ShowPlaysHandler OnDisplayPlays;

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
		private TileControl[,] m_boardTiles = new TileControl[GRID_SIZE, GRID_SIZE];

		private List<string> m_letterBag = new List<string>();
		private IList<string> m_words = new List<string>();

		private List<TileControl> m_currentWordTiles = new List<TileControl>();
		private List<TileControl> m_playedTiles = new List<TileControl>();
		private List<TileControl> m_justPlayedTiles = new List<TileControl>();
		private List<TileControl> m_panelTiles = new List<TileControl>();
		private List<TileControl> m_computersTiles = new List<TileControl>();
		private Dictionary<string, bool> m_activePlayer = new Dictionary<string, bool>();

		private TileControl m_tileBeingExchanged = null;

		private Grid m_tilePanel = null;
		private Image m_gameBoard = null;
		private Canvas m_tileCanvas = null;
		private UpwordsNetworking m_localNetwork = null;

		public UpwordsGame(Grid tilePanel, Image gameBoard, Canvas tileCanvas, UpwordsNetworking localNetwork)
		{
			m_tilePanel = tilePanel;
			m_gameBoard = gameBoard;
			m_tileCanvas = tileCanvas;
			m_localNetwork = localNetwork;

			m_localNetwork.OnReadyToStartReceivedReceived += LocalNetwork_OnReadyToStartReceivedReceived;
			m_localNetwork.OnSetActivePlayerReceived += LocalNetwork_OnSetActivePlayerReceived;
			m_localNetwork.OnPlayersTurnDetailsReceived += LocalNetwork_OnPlayersTurnDetailsReceived;
			m_localNetwork.OnLettersReceived += LocalNetwork_OnLettersReceived;
			m_localNetwork.OnLetterExchange += LocalNetwork_OnLetterExchange;

			m_randomiser = new Random((int)DateTime.Now.TimeOfDay.TotalSeconds);

			ReadWords();
		}

		public void InitialiseGame()
		{
			if (GameSettings.Settings.GameCreated)
			{
				int player = 0;
				int startPlayer = GetStartPlayer(GameSettings.Settings.PlayersJoined.Count + 1);

				FillLetterBag();
				AddLettersToPanel(GetLetters(7));

				m_activePlayer.Add(m_localNetwork.IpAddress, startPlayer == 0);

				foreach (string playerIP in GameSettings.Settings.PlayersJoined.Keys)
				{
					m_localNetwork.StartGame(playerIP);

					player++;
					m_activePlayer.Add(playerIP, startPlayer == player);
				}
			}
			else
			{
				// Let the game controller know we are ready to start.
				m_localNetwork.SendReadyToStart(GameSettings.Settings.CreatorsIpAddress);
			}
		}

		private async void ReadWords()
		{
			StorageFile wordsFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Words.txt", UriKind.Absolute));
			m_words = await FileIO.ReadLinesAsync(wordsFile);
		}


		void LocalNetwork_OnReadyToStartReceivedReceived(string playersIpAddress)
		{
			m_localNetwork.SendLetters(playersIpAddress, GetLetters(7));
			SetActivePlayer();
		}

		void LocalNetwork_OnSetActivePlayerReceived(bool active)
		{
			if (active)
			{
				IsPlayersTurn();
			}
			else
			{
				IsOpponentsTurn();
			}
		}

		void LocalNetwork_OnPlayersTurnDetailsReceived(string playersIpAddress, PlayersTurnDetails iPlayersTurnDetails)
		{
			DisplayPlay(iPlayersTurnDetails);

			if(GameSettings.Settings.GameCreated)
			{
				foreach (string playerIP in GameSettings.Settings.PlayersJoined.Keys)
				{
					if(playerIP != playersIpAddress)
					{
						m_localNetwork.SendPlayersTurnDetails(playerIP, iPlayersTurnDetails);
					}
					else if (iPlayersTurnDetails.LettersPlayed.Count < 7)
					{
						m_localNetwork.SendLetters(playerIP, GetLetters(iPlayersTurnDetails.LettersPlayed.Count));
					}
				}
				NextActivePlayer();
				SetActivePlayer();
			}
		}

		void LocalNetwork_OnLettersReceived(string serverIP, List<string> letters)
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
		
		void LocalNetwork_OnLetterExchange(string senderIP, TileDetails letter)
		{
			m_localNetwork.SendLetters(senderIP, GetLetters(1));
			m_letterBag.Add(letter.Letter);
		}

		public void SizeChanged()
		{
			m_tileSize = m_gameBoard.ActualWidth / GRID_SIZE;

			foreach (TileControl tile in m_playedTiles)
			{
				PlaceTileOnBoard(tile, tile.GridX, tile.GridY, false);
			}
			foreach (TileControl tile in m_currentWordTiles)
			{
				PlaceTileOnBoard(tile, tile.GridX, tile.GridY, false);
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
			m_letterBag.Clear();

			foreach (UpWordsLetterConfig upWordsLetter in s_UpWordsLetters)
			{
				for (int i = 0; i < upWordsLetter.NumberOf; i++)
				{
					m_letterBag.Add(upWordsLetter.Letter);
				}
			}
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
				else if (m_activePlayer[playerIP])
				{
					playerToDeactivate = playerIP;
					activateNext = true;
				}
			}

			if(playerToDeactivate.Length > 0)
			{
				m_activePlayer[playerToDeactivate] = false;
			}
			if (playerToActivate.Length > 0)
			{
				m_activePlayer[playerToActivate] = true;
			}
			else
			{
				m_activePlayer[firstIP] = true;
			}
		}

		private void SetActivePlayer()
		{
			foreach (string playerIP in m_activePlayer.Keys)
			{
				if (playerIP == m_localNetwork.IpAddress)
				{
					if(m_activePlayer[playerIP])
					{
						IsPlayersTurn();
					}
					else
					{
						IsOpponentsTurn();
					}
				}
				else
				{
					m_localNetwork.SetActivePlayer(playerIP, m_activePlayer[playerIP]);
				}
			}
		}

		private string NextRandomLetter()
		{
			int letterIndex = m_randomiser.Next(m_letterBag.Count - 1);
			string letter = m_letterBag[letterIndex];

			m_letterBag.RemoveAt(letterIndex);

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

			PlaceTileOnBoard(tile, tileData.GridX, tileData.GridY, false);
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

		public List<string> GetLetters(int numberOfLetters)
		{
			List<string> letters = new List<string>();

			numberOfLetters = Math.Min(numberOfLetters, m_letterBag.Count);

			for (int i = 0; i < numberOfLetters; i++)
			{
				letters .Add(NextRandomLetter());
			}

			return letters;
		}

		public void AddLettersToPanel(List<string> letters)
		{
			for (int i = 0; i < letters.Count; i++)
			{
				AddTileToPanel(letters[i], i);
			}
		}

		public int GetStartPlayer(int numberOfPlayers)
		{
			return m_randomiser.Next(numberOfPlayers);
		}

		public void IsPlayersTurn()
		{
			m_turnState = eTurnState.PlayersTurn;
			if(TurnIndicator != null)
			{
				TurnIndicator("Your Turn");
			}
		}

		public void IsOpponentsTurn()
		{
			m_turnState = eTurnState.OpponentsTurn;
			if (TurnIndicator != null)
			{
				TurnIndicator("Waiting on Opponent");
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

					PlaceTileOnBoard(draggedItem, gridX, gridY);
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
			else if (!up && gridY < GRID_SIZE - 1 && m_boardSpaceFilled[gridX, gridY + 1] == 0)
			{
				gridY++;
			}
			// Move left one if possible.
			else if (left && gridX > 0 && m_boardSpaceFilled[gridX - 1, gridY] == 0)
			{
				gridX--;
			}
			// Move right one if possible.
			else if (!left && gridX < GRID_SIZE - 1 && m_boardSpaceFilled[gridX + 1, gridY] == 0)
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
			else if (left && !up && gridX > 0 && gridY < GRID_SIZE - 1 && m_boardSpaceFilled[gridX - 1, gridY + 1] == 0)
			{
				gridX--;
				gridY++;
			}
			// Move up and right if possible.
			else if (!left && up && gridX < GRID_SIZE - 1 && gridY > 0 && m_boardSpaceFilled[gridX + 1, gridY - 1] == 0)
			{
				gridX++;
				gridY--;
			}
			// Move down and right if possible.
			else if (!left && !up && gridX < GRID_SIZE - 1 && gridY < GRID_SIZE - 1 && m_boardSpaceFilled[gridX + 1, gridY + 1] == 0)
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
				if (!spaceFound && !up && gridY < 13)
				{
					for (int row = gridY; row < GRID_SIZE - 1; row++)
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
				if (!spaceFound && !left && gridX < 13)
				{
					for (int column = gridX; column < GRID_SIZE - 1; column++)
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


		private void PlaceTileOnBoard(TileControl draggedItem, int gridX, int gridY, bool setLayer = true)
		{
			TranslateTransform tileRenderTransform = draggedItem.RenderTransform as TranslateTransform;

			GeneralTransform boardTransform = m_gameBoard.TransformToVisual(m_tileCanvas);
			Rect boardRect = boardTransform.TransformBounds(new Rect(0, 0, m_gameBoard.ActualWidth, m_gameBoard.ActualHeight));
			Point positionOfBoard = boardTransform.TransformPoint(new Point(0, 0));

			tileRenderTransform.X = positionOfBoard.X + (m_tileSize * gridX);// +boardRect.Width * 0.01 - (boardRect.Width * 0.005 * draggedItem.Layer);
			tileRenderTransform.Y = positionOfBoard.Y + (m_tileSize * gridY);// +boardRect.Height * 0.01 - (boardRect.Height * 0.005 * draggedItem.Layer);

			if (draggedItem.TileStatus != eTileState.JustPlayed &&
				draggedItem.GridY != ON_PANEL && draggedItem.GridX >= 0 && draggedItem.GridY >= 0)
			{
				m_boardSpaceFilled[draggedItem.GridX, draggedItem.GridY]--;
			}

			draggedItem.Width = m_tileSize;
			draggedItem.Height = m_tileSize;
			draggedItem.GridX = gridX;
			draggedItem.GridY = gridY;
			draggedItem.Visibility = Windows.UI.Xaml.Visibility.Visible;

			m_boardSpaceFilled[gridX, gridY]++;

			if (setLayer)
			{
				draggedItem.Layer = m_boardSpaceFilled[gridX, gridY];
			}

			Canvas.SetZIndex(draggedItem, draggedItem.Layer);

			if (draggedItem.TileStatus == eTileState.OnPlayerPanel)
			{
				draggedItem.TileStatus = eTileState.ComposingNewWord;
				m_panelTiles.Remove(draggedItem);
				m_currentWordTiles.Add(draggedItem);
			}
		}

		#endregion Letter Dragging Functionality

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
				if (playedWord.Word.Length > 1)
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
					if (firstTile.GridX != sourceList[index].GridX)
					{
						ShowMessage("The tiles are not in a line!");
						destinationList = null;
						return false;
					}
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
					if (firstTile.GridY != sourceList[index].GridY)
					{
						ShowMessage("The tiles are not in a line!");
						destinationList = null;
						return false;
					}
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
							break;
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
							break;
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

					if (wordCreated.Word.Length > 1)
					{
						playedWords.Add(wordCreated);
					}
				}
				// If there are letters on the board below the played tile then start with the played tile and go from there.
				else if (tile.GridY < GRID_SIZE - 1 && m_boardTiles[tile.GridX, tile.GridY + 1] != null)
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

					if (wordCreated.Word.Length > 1)
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

					if (wordCreated.Word.Length > 1)
					{
						playedWords.Add(wordCreated);
					}
				}
				// If there are letters on the board to the right of the played tile then start with the played tile and go from there.
				else if (tile.GridX < GRID_SIZE - 1 && m_boardTiles[tile.GridX + 1, tile.GridY] != null)
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

					if (wordCreated.Word.Length > 1)
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
				bool touchesExistingWord = false;
				List<TileControl> coveredTiles = new List<TileControl>();
				List<TileControl> sortedCoveredTiles = new List<TileControl>();

				foreach (TileControl tile in m_currentWordTiles)
				{
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
				}
				if (!touchesExistingWord)
				{
					ShowMessage("Your word must interact with an existing word.");
					return false;
				}

				if (coveredTiles.Count > 0)
				{
					bool horizontal = SortTiles(coveredTiles, ref sortedCoveredTiles);
					bool entireWordCovered = true;
					TileControl firstTile = sortedCoveredTiles[0];
					TileControl lastTile = sortedCoveredTiles[sortedCoveredTiles.Count - 1];

					if (horizontal)
					{
						if (firstTile.GridX > 0)
						{
							if (m_boardTiles[firstTile.GridX - 1, firstTile.GridY] != null)
							{
								entireWordCovered = false;
							}
						}
						if (firstTile.GridX < GRID_SIZE - 2)
						{
							if (m_boardTiles[firstTile.GridX + 1, firstTile.GridY] != null)
							{
								entireWordCovered = false;
							}
						}
					}
					else
					{
						if (firstTile.GridY > 0)
						{
							if (m_boardTiles[firstTile.GridX, firstTile.GridY - 1] != null)
							{
								entireWordCovered = false;
							}
						}
						if (firstTile.GridY < GRID_SIZE - 2)
						{
							if (m_boardTiles[firstTile.GridX, firstTile.GridY + 1] != null)
							{
								entireWordCovered = false;
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

					if (GameSettings.Settings.GameCreated)
					{
						foreach (string playerIP in GameSettings.Settings.PlayersJoined.Keys)
						{
							m_localNetwork.SendPlayersTurnDetails(playerIP, iPlayersTurnDetails);
						}

						AddLettersToPanel(GetLetters(replacementTiles));
						NextActivePlayer();
						SetActivePlayer();
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
						  " points from the word" + (iPlayersTurnDetails.PlayedWords.Count > 1 ? "s " : " ") + message +
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

	}
}
