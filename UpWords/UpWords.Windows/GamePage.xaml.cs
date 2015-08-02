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
		private const int MAX_TILE_ON_SPACE = 5;
		private const int GRID_SIZE = 10;
		private const int ON_PANEL = 99;

		private bool m_firstWord = true;
		private bool m_changingALetter = false;
		private bool[] m_panelSpaceFilled = new bool[7];
		private int[,] m_boardSpaceFilled = new int[GRID_SIZE, GRID_SIZE];
		private double m_tileSize = 20;
		private App m_parentClass = null;
		private Random m_randomiser = null;
		private DateTime m_messageDisplayTime = DateTime.Now;
		private DateTime m_gameStartTime = DateTime.Now;
		private eTurnState m_turnState = eTurnState.Unknown;
		private TileControl[,] m_boardTiles = new TileControl[GRID_SIZE, GRID_SIZE];
		private DispatcherTimer m_gameTimer = new DispatcherTimer();

		private List<string> m_letterBag = new List<string>();
		//private List<TileControl> m_letterBag = new List<TileControl>();
		private List<TileControl> m_currentWordTiles = new List<TileControl>();
		private List<TileControl> m_playedTiles = new List<TileControl>();
		private List<TileControl> m_justPlayedTiles = new List<TileControl>();
		private List<TileControl> m_panelTiles = new List<TileControl>();
		private List<TileControl> m_computersTiles = new List<TileControl>();

		private TileControl m_tileBeingExchanged = null;

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

		public GamePage()
		{
			this.InitializeComponent();

			m_randomiser = new Random((int)DateTime.Now.TimeOfDay.TotalSeconds);

			m_gameTimer.Tick += GameTimer_Tick;
			m_gameTimer.Interval = new TimeSpan(0, 0, 1);
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			if (e.Parameter is App)
			{
				m_parentClass = e.Parameter as App;
			}
			m_gameTimer.Start();
		}

		private void MainGrid_Loaded(object sender, RoutedEventArgs e)
		{
			StartNewGame();
		}

		private void UpWordsBoard_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			m_tileSize = UpWordsBoard.ActualWidth / GRID_SIZE;

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

		private void FillLetterBag()
		{
			m_letterBag.Clear();

			foreach (UpWordsLetterConfig upWordsLetter in s_UpWordsLetters)
			{
				for (int i = 0; i < upWordsLetter.NumberOf; i++)
				{
					m_letterBag.Add(upWordsLetter.Letter);
					/*
					TileControl tile = new TileControl(upWordsLetter.Letter);
					tile.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
					tile.RenderTransform = new TranslateTransform();
					tile.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Left;
					tile.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Top;
					TileCanvas.Children.Add(tile);

					m_letterBag.Add(tile);
					*/
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

		private void AddTileToPanel(string letter, int position)
		{
			TileControl tile = new TileControl(letter);
			tile.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
			tile.RenderTransform = new TranslateTransform();
			tile.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Left;
			tile.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Top;
			TileCanvas.Children.Add(tile);

			tile.TileStatus = eTileState.OnPlayerPanel;
			tile.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
			tile.ManipulationStarting += DragLetter_ManipulationStarting;
			tile.ManipulationDelta += DragLetter_ManipulationDelta;
			tile.ManipulationCompleted += DragLetter_ManipulationCompleted;

			PlaceTileOnPanel(tile, position);

			m_panelTiles.Add(tile);
		}

		private void StartNewGame()
		{
			FillLetterBag();

			GeneralTransform boardTransform = UpWordsBoard.TransformToVisual(TileCanvas);

			for (int i = 0; i < 7; i++)
			{
				AddTileToPanel(NextRandomLetter(), i);
			}

			m_turnState = eTurnState.PlayersTurn;
			/*
			if (m_randomiser.Next(100) > 50)
			{
				m_turnState = eTurnState.PlayersTurn;
				//MessageTextBox.Text = "You won the draw, it is your turn to place a word.";
				//MessageTextBox.Visibility = Windows.UI.Xaml.Visibility.Visible;
				m_messageDisplayTime = DateTime.Now;
			}
			else
			{
				m_turnState = eTurnState.ComputersTurn;
				//MessageTextBox.Text = "The Computer won the draw.";
				//MessageTextBox.Visibility = Windows.UI.Xaml.Visibility.Visible;
				m_messageDisplayTime = DateTime.Now;
			}
			m_firstWord = true;
			m_gameStartTime = DateTime.Now;
			m_gameTimer.Start();
			*/
		}

		void GameTimer_Tick(object sender, object e)
		{
			if (!m_changingALetter && 
				DateTime.Now.Subtract(m_messageDisplayTime).TotalSeconds > 5 && MessageTextBox.Visibility == Windows.UI.Xaml.Visibility.Visible)
			{
				HideMessage();
			}
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

		#region Letter Dragging Functionality

		void DragLetter_ManipulationStarting(object sender, ManipulationStartingRoutedEventArgs e)
		{
			if (m_turnState != eTurnState.PlayersTurn)
			{
				e.Handled = true;
			}
			else if(m_changingALetter)
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
				tileRenderTransform.Y += e.Delta.Translation.Y;

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

				GeneralTransform imageTrasform = draggedItem.TransformToVisual(TileCanvas);
				Point imagePosition = imageTrasform.TransformPoint(new Point());
				Point cursorPosition = imageTrasform.TransformPoint(e.Position);
				Rect imageRect = imageTrasform.TransformBounds(new Rect(0, 0, draggedItem.ActualWidth, draggedItem.ActualHeight));

				GeneralTransform boardTransform = UpWordsBoard.TransformToVisual(TileCanvas);
				Rect boardRect = boardTransform.TransformBounds(new Rect(0, 0, UpWordsBoard.ActualWidth, UpWordsBoard.ActualHeight));
				GeneralTransform tilePanelTransform = TilePanel.TransformToVisual(TileCanvas);
				Rect letterPanelRect = tilePanelTransform.TransformBounds(new Rect(0, 0, TilePanel.ActualWidth, TilePanel.ActualHeight));

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

			foreach (TileControl tile in m_panelTiles)
			{
				// If a tile is dropped on an existing tile...
				if (tile != draggedItem && tile.GridX == gridX)
				{
					// If moving tiles that are already on the panel then shuffle up or down as appropriate
					if (draggedItem.GridY == ON_PANEL)
					{
						// If the tile is being moved right then shuffle all preceeding tiles right
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
						// If the tile is being moved left then shuffle all preceeding tiles right
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

			GeneralTransform tilePanelTransform = TilePanel.TransformToVisual(TileCanvas);
			Point positionOfBoard = tilePanelTransform.TransformPoint(new Point(0, 0));

			tileRenderTransform.X = positionOfBoard.X + TilePanel.ActualWidth /2 - (m_tileSize * (3.5 - gridX));
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

			GeneralTransform boardTransform = UpWordsBoard.TransformToVisual(TileCanvas);
			Rect boardRect = boardTransform.TransformBounds(new Rect(0, 0, UpWordsBoard.ActualWidth, UpWordsBoard.ActualHeight));
			Point positionOfBoard = boardTransform.TransformPoint(new Point(0, 0));

			if (setLayer)
			{
				draggedItem.Layer = m_boardSpaceFilled[gridX, gridY];
			}

			tileRenderTransform.X = positionOfBoard.X + (m_tileSize * gridX);// +boardRect.Width * 0.01 - (boardRect.Width * 0.005 * draggedItem.Layer);
			tileRenderTransform.Y = positionOfBoard.Y + (m_tileSize * gridY);// +boardRect.Height * 0.01 - (boardRect.Height * 0.005 * draggedItem.Layer);

			if (draggedItem.GridY != ON_PANEL && draggedItem.GridX >= 0 && draggedItem.GridY >= 0)
			{
				m_boardSpaceFilled[draggedItem.GridX, draggedItem.GridY]--;
			}

			draggedItem.Width = m_tileSize;
			draggedItem.Height = m_tileSize;
			draggedItem.GridX = gridX;
			draggedItem.GridY = gridY;
			draggedItem.Visibility = Windows.UI.Xaml.Visibility.Visible;

			m_boardSpaceFilled[gridX, gridY]++;

			Canvas.SetZIndex(draggedItem, draggedItem.Layer);

			if (draggedItem.TileStatus == eTileState.OnPlayerPanel)
			{
				draggedItem.TileStatus = eTileState.ComposingNewWord;
				m_panelTiles.Remove(draggedItem);
				m_currentWordTiles.Add(draggedItem);
			}
		}

		#endregion Letter Dragging Functionality

		#region Button events.

		private void SubmitButton_Click(object sender, RoutedEventArgs e)
		{
			HideMessage();

			if (m_currentWordTiles.Count == 0)
			{
				ShowMessage("You have not placed any letters!");
			}
			else
			{
				if(m_firstWord)
				{
					bool goesThroughCenter = false;
					foreach(TileControl tile in m_currentWordTiles)
					{
						if(tile.GridX >3 && tile.GridX < 6 && tile.GridY >3 && tile.GridY < 6)
						{
							goesThroughCenter = true;
							break;
						}
					}
					if (!goesThroughCenter)
					{
						ShowMessage("The first word must go through the center four squares!");
						return;
					}
				}
				bool allWordsOk = true;
				List<string> playedWords = GetPlayedWords();

				if (playedWords.Count == 0)
				{
					return;
				}
				
				foreach (string playedWord in playedWords)
				{
					if (playedWord.Length > 0)
					{
						if (!m_parentClass.Words.Contains(playedWord))
						{
							ShowMessage("My dictionary does not contain the word '" + playedWord + "'.");
							allWordsOk = false;
							break;
						}
					}
				}

				if (allWordsOk)
				{
					/*
					foreach (string playedWord in playedWords)
					{
						PlayersWords.Items.Add(playedWord);
					}
					*/
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
					}

					m_firstWord = false;
					m_currentWordTiles.Clear();
					//m_turnState = eTurnState.ComputersTurn;

					for (int i = 7 - m_panelTiles.Count; i > 0; i--)
					{
						AddTileToPanel(NextRandomLetter(), i / 2);
					}
				}
			}
		}

		private void ShuffleLettersButton_Click(object sender, RoutedEventArgs e)
		{
			HideMessage();

			List<int> positions = new List<int>() { 0, 1, 2, 3, 4, 5, 6 };
			GeneralTransform boardTransform = UpWordsBoard.TransformToVisual(this);
			Point positionOfBoard = boardTransform.TransformPoint(new Point(0, 0));

			foreach (TileControl tile in m_panelTiles)
			{
				int selector = m_randomiser.Next(positions.Count);

				PlaceTileOnPanel(tile, positions[selector]);

				positions.RemoveAt(selector);
			}
		}

		private void ChangeLetterButton_Click(object sender, RoutedEventArgs e)
		{
			if (m_changingALetter)
			{
				ChangeLetterButton.Content = "Change a Letter";
				m_changingALetter = false;
				HideMessage();
			}
			else
			{
				ChangeLetterButton.Content = "Cancel Change";
				m_changingALetter = true;
				ShowMessage("Select the letter you wish to exchange.\r\nThis will end your turn.");
			}
		}

		private void RecallLettersButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void SkipTurnButton_Click(object sender, RoutedEventArgs e)
		{

		}

		private void ResignGameButton_Click(object sender, RoutedEventArgs e)
		{

		}

		#endregion Button events.

		#region Server interactions.

		private void ExchangeTile(TileControl tileToExchange)
		{
			m_tileBeingExchanged = tileToExchange;

			// Request tile exchange.
			// ...
			m_letterBag.Add(tileToExchange.Letter);
			ReplacementLetterReceived(NextRandomLetter());
		}

		private void ReplacementLetterReceived(string newLetter)
		{
			if (m_changingALetter)
			{
				int position = m_tileBeingExchanged.GridX;

				m_panelTiles.Remove(m_tileBeingExchanged);

				AddTileToPanel(newLetter, position);

				ChangeLetterButton.Content = "Change a Letter";
				m_changingALetter = false;
				HideMessage();
			}
		}

		#endregion Server interactions.

		#region Word finding functionality.

		private List<string> GetPlayedWords()
		{
			bool horizontal = true;
			string playedWord = string.Empty;
			List<string> playedWords = new List<string>();
			List<TileControl> playedTiles = new List<TileControl>();

			horizontal = SortCurrentWordTiles(ref playedTiles);

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

			if (playedWord.Length > 0)
			{
				playedWords.Add(playedWord);
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

		private bool SortCurrentWordTiles(ref List<TileControl> playedTiles)
		{
			int sortIndex = 0;
			bool horizontal = true;
			TileControl firstTile = m_currentWordTiles[0];

			playedTiles.Add(firstTile);

			// Sort the letters into the order in which they appear on the board.
			for (int index = 1; index < m_currentWordTiles.Count; index++)
			{
				if (firstTile.GridX == m_currentWordTiles[index].GridX)
				{
					if (firstTile.GridX != m_currentWordTiles[index].GridX)
					{
						ShowMessage("The tiles are not in a line!");
						playedTiles = null;
						return false;
					}
					horizontal = false;
					for(sortIndex = 0; sortIndex < playedTiles.Count; sortIndex++)
					{
						if(playedTiles[sortIndex].GridY > m_currentWordTiles[index].GridY)
						{
							break;
						}
					}
					playedTiles.Insert(sortIndex, m_currentWordTiles[index]);
				}
				else
				{
					if (firstTile.GridY != m_currentWordTiles[index].GridY)
					{
						ShowMessage("The tiles are not in a line!");
						playedTiles = null;
						return false;
					}
					horizontal = true;
					for (sortIndex = 0; sortIndex < playedTiles.Count; sortIndex++)
					{
						if (playedTiles[sortIndex].GridX > m_currentWordTiles[index].GridX)
						{
							break;
						}
					}
					playedTiles.Insert(sortIndex, m_currentWordTiles[index]);
				}
			}

			return horizontal;
		}

		private string GetMainWordPlayedHorizontal(List<TileControl> playedTiles)
		{
			int index;
			int lastX = -1;

			// Find the word that has been played.
			string playedWord = playedTiles[0].Letter;

			// Prepend any letters that appear before the played word.
			if (playedTiles[0].GridX > 0)
			{
				int previousX = playedTiles[0].GridX - 1;
				while (previousX >= 0 && m_boardTiles[previousX, playedTiles[0].GridY] != null)
				{
					playedWord = m_boardTiles[previousX, playedTiles[0].GridY].Letter + playedWord;
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
							playedWord += m_boardTiles[X, playedTiles[index].GridY].Letter;
						}
						else
						{
							ShowMessage("There seems to be a gap in your word!");
							playedWord = string.Empty;
							break;
						}
					}
				}

				// Add the current tile.
				playedWord += playedTiles[index].Letter;

				// Save the last X value;
				lastX = playedTiles[index].GridX;
			}

			// Append any letters that appear after the played word.
			if (lastX < GRID_SIZE)
			{
				int nextX = lastX + 1;
				while (nextX < GRID_SIZE && m_boardTiles[nextX, playedTiles[0].GridY] != null)
				{
					playedWord += m_boardTiles[nextX, playedTiles[0].GridY].Letter;
					nextX++;
				}
			}

			return playedWord;
		}

		private string GetMainWordPlayedVertical(List<TileControl> playedTiles)
		{
			int index;
			int lastY = -1;

			// Find the word that has been played.
			string playedWord = playedTiles[0].Letter;

			// Prepend any letters that appear before the played word.
			if (playedTiles[0].GridY > 0)
			{
				int previousY = playedTiles[0].GridY - 1;
				while (previousY >= 0 && m_boardTiles[playedTiles[0].GridX, previousY] != null)
				{
					playedWord = m_boardTiles[playedTiles[0].GridX, previousY].Letter + playedWord;
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
							playedWord += m_boardTiles[playedTiles[index].GridX, Y].Letter;
						}
						else
						{
							ShowMessage("There seems to be a gap in your word!");
							playedWord = string.Empty;
							break;
						}
					}
				}
				// Add the current tile.
				playedWord += playedTiles[index].Letter;

				// Save the last Y value;
				lastY = playedTiles[index].GridY;
			}

			// Append any letters that appear after the played word.
			if (lastY < GRID_SIZE)
			{
				int nextY = lastY + 1;
				while (nextY < GRID_SIZE && m_boardTiles[playedTiles[0].GridX, nextY] != null)
				{
					playedWord += m_boardTiles[playedTiles[0].GridX, nextY].Letter;
					nextY++;
				}
			}

			return playedWord;
		}

		private void GetSideWordsForWordPlayedHorizontal(List<TileControl> playedTiles, ref List<string> playedWords)
		{
			foreach (TileControl tile in playedTiles)
			{
				if (tile.GridY > 0 && m_boardTiles[tile.GridX, tile.GridY - 1] != null)
				{
					int Y = tile.GridY - 1;
					string word = string.Empty;

					// Work up the column until an empty space is found.
					while (Y > 0 && m_boardTiles[tile.GridX, Y - 1] != null)
					{
						Y--;
					}
					// Now work back down to build up the word.
					while (Y < GRID_SIZE - 1 && (m_boardTiles[tile.GridX, Y] != null || Y == tile.GridY))
					{
						if (Y == tile.GridY)
						{
							word += tile.Letter;
						}
						else
						{
							word += m_boardTiles[tile.GridX, Y].Letter;
						}
						Y++;
					}

					playedWords.Add(word);
				}
				else if (tile.GridY < GRID_SIZE-2 && m_boardTiles[tile.GridX, tile.GridY + 1] != null)
				{
					int Y = tile.GridY + 1;
					string word = tile.Letter;

					// Now work back down to build up the word.
					while (Y < GRID_SIZE - 1 && m_boardTiles[tile.GridX, Y] != null)
					{
						word += m_boardTiles[tile.GridX, Y].Letter;
						Y++;
					}

					playedWords.Add(word);
				}
			}
		}

		private void GetSideWordsForWordPlayedVertical(List<TileControl> playedTiles, ref List<string> playedWords)
		{
			foreach (TileControl tile in playedTiles)
			{
				if (tile.GridX > 0 && m_boardTiles[tile.GridX - 1, tile.GridY] != null)
				{
					int X = tile.GridX - 1;
					string word = string.Empty;

					// Work back along the row until an empty space is found.
					while (X > 0 && m_boardTiles[X - 1, tile.GridY] != null)
					{
						X--;
					}
					// Now work forward to build up the word.
					while (X < GRID_SIZE - 1 && (m_boardTiles[X, tile.GridY] != null || X == tile.GridX))
					{
						if (X == tile.GridX)
						{
							word += tile.Letter;
						}
						else
						{
							word += m_boardTiles[X, tile.GridY].Letter;
						}
						X++;
					}

					playedWords.Add(word);
				}
				else if (tile.GridX < GRID_SIZE - 2 && m_boardTiles[tile.GridX + 1, tile.GridY] != null)
				{
					int X = tile.GridX + 1;
					string word = tile.Letter;

					// Now work forward to build up the word.
					while (X < GRID_SIZE - 1 && m_boardTiles[X, tile.GridY] != null)
					{
						word += m_boardTiles[X, tile.GridY].Letter;
						X++;
					}

					playedWords.Add(word);
				}
			}
		}

		#endregion Word finding functionality.


	}
}
