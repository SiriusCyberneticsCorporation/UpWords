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
		private const int GRID_SIZE = 10;
		private const int ON_PANEL = 99;

		private bool[] m_panelSpaceFilled = new bool[7];
		private int[,] m_boardSpaceFilled = new int[GRID_SIZE, GRID_SIZE];
		private double m_tileSize = 20;
		private Random m_randomiser = null;
		private DateTime m_messageDisplayTime = DateTime.Now;
		private DateTime m_gameStartTime = DateTime.Now;
		private eTurnState m_turnState = eTurnState.Unknown;
		private TileControl[,] m_boardTiles = new TileControl[GRID_SIZE, GRID_SIZE];
		private List<TileControl> m_letterBag = new List<TileControl>();
		private List<TileControl> m_currentWordTiles = new List<TileControl>();
		private List<TileControl> m_playedTiles = new List<TileControl>();
		private List<TileControl> m_panelTiles = new List<TileControl>();
		private List<TileControl> m_computersTiles = new List<TileControl>();

		#region UpWords Configuration

		private class UpWordsLetterConfig
		{
			public int NumberOf;
			public string Letter;
		}

		private static List<UpWordsLetterConfig> s_UpWordsLetters = new List<UpWordsLetterConfig>()
		{
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
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "Q"},
			new UpWordsLetterConfig() {NumberOf = 2, Letter = "R"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "S"},
			new UpWordsLetterConfig() {NumberOf = 4, Letter = "T"},
			new UpWordsLetterConfig() {NumberOf = 3, Letter = "U"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "V"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "W"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "X"},
			new UpWordsLetterConfig() {NumberOf = 2, Letter = "Y"},
			new UpWordsLetterConfig() {NumberOf = 1, Letter = "Z"},
		};

		#endregion UpWords Configuration

		public GamePage()
		{
			this.InitializeComponent();

			m_randomiser = new Random((int)DateTime.Now.TimeOfDay.TotalSeconds);
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			//bool.TryParse(e.Parameter as string, out m_restartLastGame);
		}

		private void MainGrid_Loaded(object sender, RoutedEventArgs e)
		{
			StartNewGame();
		}

		private void UpWordsBoard_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			m_tileSize = 0.98 * UpWordsBoard.ActualWidth / GRID_SIZE;

			foreach (TileControl tile in m_playedTiles)
			{
				PlaceTileOnBoard(tile, tile.GridX, tile.GridY);
			}
			foreach (TileControl tile in m_currentWordTiles)
			{
				PlaceTileOnBoard(tile, tile.GridX, tile.GridY);
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
					TileControl tile = new TileControl(upWordsLetter.Letter);
					tile.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
					tile.RenderTransform = new TranslateTransform();
					tile.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Left;
					tile.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Top;
					TileCanvas.Children.Add(tile);

					m_letterBag.Add(tile);
				}
			}
		}

		private void StartNewGame()
		{
			FillLetterBag();

			GeneralTransform boardTransform = UpWordsBoard.TransformToVisual(TileCanvas);

			for (int i = 0; i < 7; i++)
			{
				int nextLetter = m_randomiser.Next(m_letterBag.Count - 1);

				TileControl tile = m_letterBag[nextLetter];

				tile.TileStatus = eTileState.OnPlayerPanel;
				tile.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
				tile.ManipulationStarting += DragLetter_ManipulationStarting;
				tile.ManipulationDelta += DragLetter_ManipulationDelta;
				tile.ManipulationCompleted += DragLetter_ManipulationCompleted;

				PlaceTileOnPanel(tile, i);

				m_panelTiles.Add(tile);

				m_letterBag.RemoveAt(nextLetter);
			}

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
			/*
			m_firstWord = true;
			m_gameStartTime = DateTime.Now;
			m_gameTimer.Start();
			*/
		}

		#region Letter Dragging Functionality

		void DragLetter_ManipulationStarting(object sender, ManipulationStartingRoutedEventArgs e)
		{
			if (m_turnState != eTurnState.PlayersTurn)
			{
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

					draggedItem.Layer = 0;
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
					gridX = Math.Max(0, Math.Min(14, (int)((1 + desiredX - boardRect.X) / m_tileSize)));
					gridY = Math.Max(0, Math.Min(14, (int)((1 + desiredY - boardRect.Y) / m_tileSize)));

					draggedItem.Layer = m_boardSpaceFilled[gridX, gridY];

					PlaceTileOnBoard(draggedItem, gridX, gridY);
				}
				else
				{
					draggedItem.Layer = 0;
					PlaceTileOnPanel(draggedItem, 0);
				}

				TileMoved();
			}
			catch (Exception ex)
			{
				int x = 0;
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


		private void PlaceTileOnBoard(TileControl draggedItem, int gridX, int gridY)
		{
			TranslateTransform tileRenderTransform = draggedItem.RenderTransform as TranslateTransform;

			GeneralTransform boardTransform = UpWordsBoard.TransformToVisual(TileCanvas);
			Rect boardRect = boardTransform.TransformBounds(new Rect(0, 0, UpWordsBoard.ActualWidth, UpWordsBoard.ActualHeight));
			Point positionOfBoard = boardTransform.TransformPoint(new Point(0, 0));

			tileRenderTransform.X = positionOfBoard.X + (m_tileSize * gridX) + boardRect.Width * 0.01 - (boardRect.Width * 0.005 * draggedItem.Layer);
			tileRenderTransform.Y = positionOfBoard.Y + (m_tileSize * gridY) + boardRect.Height * 0.01 - (boardRect.Height * 0.005 * draggedItem.Layer);

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
	}
}
