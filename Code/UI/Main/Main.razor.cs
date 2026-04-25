using Sandbox;
using Sandbox.UI;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

// Подключаем общее пространство имен, чтобы наши Razor-компоненты видели этот код
namespace Lottery;

/// <summary>
/// Главный контроллер интерфейса. Хранит баланс, управляет покупками билетов, 
/// апгрейдами (машинками) и обрабатывает перетаскивание билетов.
/// </summary>
public partial class Main : PanelComponent
{
	// Массив всплывающего текста (например, "+$1.00" над билетом)
	public List<FloatingText> Floats { get; set; } = new();

	// Текущий баланс игрока. Аннотация [Property] позволяет увидеть это поле в редакторе sbox (в инспекторе)
	[Property] public decimal Money { get; set; } = 5000.00m;

	// --- Система пассивного дохода и кликера ---
	public int IncomeLevel = 0; // Уровень пассивного дохода за клик
	public int AutoclickerLevel = 0; // Уровень авто-кликера
	public decimal IncomeCost => 15m * (decimal)Math.Pow( 1.5, IncomeLevel ); // Цена 1 апгрейда дохода (+50% за каждый)
	public decimal AutoclickerCost => 30m * (decimal)Math.Pow( 1.5, AutoclickerLevel );

	// --- Машинка: Автоматический стиратор (Scratcher) ---
	public int ScratcherSpeedLevel = 0;
	public decimal ScratcherSpeedCost => 100m * (decimal)Math.Pow( 1.5, ScratcherSpeedLevel );

	// --- Машинка: Авто-сборщик выигрышей (Opener) ---
	public int OpenerSpeedLevel = 0;
	public decimal OpenerSpeedCost => 100m * (decimal)Math.Pow( 1.5, OpenerSpeedLevel );

	// --- Машинка: Пылесос (Vacuum) - затягивает билеты ---
	public int VacuumSpeedLevel = 0;
	public decimal VacuumSpeedCost => 80m * (decimal)Math.Pow( 1.5, VacuumSpeedLevel );

	// Флаги наличия машин/апгрейдов
	public bool HasAutoclicker = false;
	public bool HasAutoScratcher = false;
	public bool HasAutoOpener = false;
	public bool HasVacuum = false;
	public bool HasVipPass = false;

	// Переменные для перетаскивания (Drag & Drop) билетов мышкой
	private ActiveTicket draggedTicket = null;
	private Vector2 dragStartPos;
	private Vector2 ticketStartPos;
	private bool isDragging = false;

	public decimal BasicTicketCost = 1m; // 1$ базовая стоимость

	// Список всех билетов, которые сейчас лежат на физическом "столе"
	public List<ActiveTicket> TicketsOnTable { get; set; } = new();

	// Билет, который мы открыли и сейчас стираем поверх экрана (модальное окно)
	public ActiveTicket OpenedTicket { get; set; }

	// История транзакций (покупки и выигрыши) для сайдбара
	public List<HistoryEntry> TicketHistory { get; set; } = new();

	// Счетчик Z-Index, нужен, чтобы новые/последние кликнутые билеты рисовались поверх остальных
	private int ticketZCounter = 1;

	// Лідерборд з мережі
	public Sandbox.Services.Leaderboards.Board2 TopPlayersBoard { get; set; }

	// ==========================================
	// СИСТЕМА ЗБЕРЕЖЕННЯ (UGC Storage) ТА ЛІДЕРБОРДІВ
	// ==========================================

	protected override void OnStart()
	{
		base.OnStart();

		// При старті гри завантажуємо прогрес
		LoadGame();

		// Запускаємо цикл фонового збереження кожні 10 секунд
		_ = AutoSaveLoop();

		// Запускаємо цикл завантаження лідерборду
		_ = UpdateLeaderboardAsync();
	}

	protected override void OnDestroy()
	{
		// Зберігаємо гру при виході
		SaveGame();
		base.OnDestroy();
	}

	private async Task UpdateLeaderboardAsync()
	{
		while ( IsValid )
		{
			try
			{
				Sandbox.Services.Stats.SetValue( "max_wealth", (double)Money );

				var board = Sandbox.Services.Leaderboards.GetFromStat( Game.Ident, "max_wealth" );
				board.SetAggregationMax();
				board.SetSortDescending();
				board.MaxEntries = 5;

				await board.Refresh();

				TopPlayersBoard = board;
				StateHasChanged();
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Leaderboard update failed: {ex.Message}" );
			}

			await GameTask.DelaySeconds( 60.0f );
		}
	}

	public void SaveGame()
	{
		var data = new GameSaveData
		{
			Money = Money,
			IncomeLevel = IncomeLevel,
			AutoclickerLevel = AutoclickerLevel,
			ScratcherSpeedLevel = ScratcherSpeedLevel,
			OpenerSpeedLevel = OpenerSpeedLevel,
			VacuumSpeedLevel = VacuumSpeedLevel,
			HasAutoclicker = HasAutoclicker,
			HasAutoScratcher = HasAutoScratcher,
			HasAutoOpener = HasAutoOpener,
			HasVacuum = HasVacuum,
			HasVipPass = HasVipPass
		};

		var saves = Storage.GetAll( "save" );
		var saveEntry = saves.FirstOrDefault();

		if ( saveEntry == null )
		{
			saveEntry = Storage.CreateEntry( "save" );
		}

		saveEntry.Files.WriteJson( "player_save.json", data );
	}

	public void LoadGame()
	{
		var saves = Storage.GetAll( "save" );
		var saveEntry = saves.FirstOrDefault();

		if ( saveEntry != null && saveEntry.Files.FileExists( "player_save.json" ) )
		{
			var data = saveEntry.Files.ReadJson<GameSaveData>( "player_save.json" );
			if ( data != null )
			{
				Money = data.Money;
				IncomeLevel = data.IncomeLevel;
				AutoclickerLevel = data.AutoclickerLevel;
				ScratcherSpeedLevel = data.ScratcherSpeedLevel;
				OpenerSpeedLevel = data.OpenerSpeedLevel;
				VacuumSpeedLevel = data.VacuumSpeedLevel;
				HasAutoclicker = data.HasAutoclicker;
				HasAutoScratcher = data.HasAutoScratcher;
				HasAutoOpener = data.HasAutoOpener;
				HasVacuum = data.HasVacuum;
				HasVipPass = data.HasVipPass;

				if ( AutoclickerLevel > 0 ) _ = AutoclickerLoop();
				if ( HasAutoScratcher ) _ = AutoScratcherLoop();
				if ( HasAutoOpener ) _ = AutoOpenerLoop();
				if ( HasVacuum ) _ = VacuumLoop();
			}
		}
	}

	private async Task AutoSaveLoop()
	{
		while ( IsValid )
		{
			await GameTask.DelaySeconds( 10.0f );
			SaveGame();
		}
	}

	// ==========================================
	// ОСНОВНА ЛОГІКА
	// ==========================================

	public string GetTicketImage( string tier )
	{
		if ( tier == "СРІБЛО" ) return "/ui/tickets/2.png";
		if ( tier == "ЗОЛОТО" ) return "/ui/tickets/3.png";
		return "/ui/tickets/1.png";
	}

	public void EarnMoney()
	{
		Manager.PlayClick(); // ЗВУК КЛІКУ

		decimal amount = 0.01m * (1m + IncomeLevel);
		if ( HasVipPass ) amount *= 10m;
		Money += amount;
	}

	public void BuyTicket( decimal cost, string tier )
	{
		if ( Money >= cost )
		{
			Manager.PlayClick(); // ЗВУК КЛІКУ
			Money -= cost;

			decimal prize = 0m;
			List<string> generatedSymbols = new();
			List<string> generatedTopNumbers = new();

			if ( tier == "ЗОЛОТО" )
			{
				List<int> topNums = new();
				while ( topNums.Count < 4 )
				{
					int r = System.Random.Shared.Next( 1, 101 );
					if ( !topNums.Contains( r ) ) topNums.Add( r );
				}
				generatedTopNumbers = topNums.Select( n => n.ToString() ).ToList();

				List<int> bottomNums = new();
				while ( bottomNums.Count < 9 )
				{
					int r = System.Random.Shared.Next( 1, 101 );
					// In original game, same number could only appear once in bottom area
					if ( !bottomNums.Contains( r ) ) bottomNums.Add( r );
				}
				
				int matches = topNums.Intersect(bottomNums).Count();
				if ( matches == 1 ) prize = cost * 1m;
				else if ( matches == 2 ) prize = cost * 9m;
				else if ( matches == 3 ) prize = cost * 27m;
				else if ( matches >= 4 ) prize = cost * 81m;

				generatedSymbols = bottomNums.Select( n => n.ToString() ).ToList();
			}
			else
			{
				string[] possible = tier == "СРІБЛО" ? new string[] {
					"/ui/tickets/symbols/brainrot1.png", "/ui/tickets/symbols/brainrot2.png", "/ui/tickets/symbols/brainrot3.png",
					"/ui/tickets/symbols/brainrot4.png", "/ui/tickets/symbols/brainrot5.png", "/ui/tickets/symbols/brainrot6.png"
				} : new string[] {
					"/ui/tickets/symbols/Action!.png", "/ui/tickets/symbols/chest.png", "/ui/tickets/symbols/energy.png",
					"/ui/tickets/symbols/gift.png", "/ui/tickets/symbols/message.png",
					"/ui/tickets/symbols/skull.png"
				};

				for ( int i = 0; i < 3; i++ )
				{
					generatedSymbols.Add( possible[System.Random.Shared.Next( 0, possible.Length )] );
				}

				if ( tier == "СРІБЛО" )
				{
					int brainrotCount = generatedSymbols.Count( x => x == "/ui/tickets/symbols/brainrot6.png" );
					if ( brainrotCount == 3 ) prize = 500m;
					else if ( brainrotCount == 2 ) prize = 100m;
					else if ( brainrotCount == 1 ) prize = 10m;
				}
				else if ( tier == "БРОНЗА" )
				{
					if ( generatedSymbols[0] == generatedSymbols[1] && generatedSymbols[1] == generatedSymbols[2] )
					{
						string winSymbol = generatedSymbols[0];
						if ( winSymbol == "/ui/tickets/symbols/energy.png" ) prize = 3m;
						else if ( winSymbol == "/ui/tickets/symbols/message.png" ) prize = 5m;
						else if ( winSymbol == "/ui/tickets/symbols/skull.png" ) prize = 10m;
						else if ( winSymbol == "/ui/tickets/symbols/gift.png" ) prize = 15m;
						else if ( winSymbol == "/ui/tickets/symbols/Action!.png" ) prize = 20m;
						else prize = 2m; // chest or any other
					}
				}
				else
				{
					// For any other unexpected tier
					if ( generatedSymbols[0] == generatedSymbols[1] && generatedSymbols[1] == generatedSymbols[2] )
					{
						prize = cost * 10m;
					}
				}
			}

			ticketZCounter++;

			float tx = 150 + System.Random.Shared.Next( 0, 300 );
			float ty = 150 + System.Random.Shared.Next( 0, 300 );
			float trot = System.Random.Shared.Next( -35, 35 );

			float tableW = 280f;
			float tableH = 416f;

			if ( tier == "СРІБЛО" )
			{
				tableW = 416f;
				tableH = 304;
			}
			if ( tier == "ЗОЛОТО" )
			{
				tableW = 240f;
				tableH = 400f;
			}

			TicketsOnTable.Add( new ActiveTicket
			{
				Prize = prize,
				Tier = tier,
				X = tx,
				Y = ty,
				Rotation = trot,
				ZIndex = ticketZCounter,
				Width = tableW,
				Height = tableH,
				Symbols = generatedSymbols,
				TopNumbers = generatedTopNumbers
			} );
		}
	}

	public void BuyUpgrade( string upgradeName, decimal cost )
	{
		if ( Money >= cost )
		{
			Manager.PlayClick(); // ЗВУК КЛІКУ

			if ( upgradeName == "Income" ) IncomeLevel++;
			else if ( upgradeName == "Autoclicker" ) { AutoclickerLevel++; if ( AutoclickerLevel == 1 ) _ = AutoclickerLoop(); }
			else if ( upgradeName == "Auto Scratcher" ) { if ( HasAutoScratcher ) return; HasAutoScratcher = true; _ = AutoScratcherLoop(); }
			else if ( upgradeName == "Scratcher Speed" ) { ScratcherSpeedLevel++; }
			else if ( upgradeName == "Auto Opener" ) { if ( HasAutoOpener ) return; HasAutoOpener = true; _ = AutoOpenerLoop(); }
			else if ( upgradeName == "Opener Speed" ) { OpenerSpeedLevel++; }
			else if ( upgradeName == "Vacuum" ) { if ( HasVacuum ) return; HasVacuum = true; _ = VacuumLoop(); }
			else if ( upgradeName == "Vacuum Speed" ) { VacuumSpeedLevel++; }
			else if ( upgradeName == "VIP Pass" ) { if ( HasVipPass ) return; HasVipPass = true; }

			Money -= cost;

			TicketHistory.Add( new HistoryEntry
			{
				Amount = -cost,
				Message = $"Bought: {upgradeName} (-${cost.ToString( "0.00" )})"
			} );

			SaveGame();
		}
	}

	// --- Обработчики мыши (Drag & Drop) ---

	public void StartDrag( ActiveTicket ticket )
	{
		ticketZCounter++;
		ticket.ZIndex = ticketZCounter;
		draggedTicket = ticket;
		dragStartPos = Panel.MousePosition;
		ticketStartPos = new Vector2( ticket.X, ticket.Y );
		isDragging = false;
	}

	public void OnMouseMove()
	{
		if ( draggedTicket != null )
		{
			var delta = Panel.MousePosition - dragStartPos;
			if ( delta.Length > 5f ) isDragging = true;

			draggedTicket.X = ticketStartPos.x + delta.x;
			draggedTicket.Y = ticketStartPos.y + delta.y;
			StateHasChanged();
		}
	}

	public void OnMouseUp()
	{
		if ( draggedTicket != null )
		{
			if ( !isDragging )
			{
				OpenTicket( draggedTicket );
			}
			else if ( HasAutoScratcher && !draggedTicket.IsScratched )
			{
				if ( Math.Abs( draggedTicket.X - 120f ) < 80 && Math.Abs( draggedTicket.Y - 360f ) < 100 )
				{
					draggedTicket.ProcessState = 1;
					draggedTicket.X = 120f + (System.Random.Shared.NextSingle() * 10f - 5f);
					draggedTicket.Y = 350f + (System.Random.Shared.NextSingle() * 10f - 5f);
					draggedTicket.Rotation = System.Random.Shared.NextSingle() * 20f - 10f;
				}
				else
				{
					draggedTicket.ProcessState = 0;
				}
			}
			draggedTicket = null;
			isDragging = false;
		}
	}

	public void OpenTicket( ActiveTicket ticket )
	{
		Manager.PlayClick(); // ЗВУК КЛІКУ
		ticketZCounter++;
		ticket.ZIndex = ticketZCounter;
		OpenedTicket = ticket;
	}

	public void ClaimPrize( ActiveTicket ticket )
	{
		if ( TicketsOnTable.Contains( ticket ) )
		{
			Money += ticket.Prize;

			TicketHistory.Add( new HistoryEntry
			{
				Amount = ticket.Prize,
				Message = ticket.Prize > 0 ? $"Win: ${ticket.Prize.ToString( "0.00" )}" : "You lost..."
			} );

			TicketsOnTable.Remove( ticket );
			StateHasChanged();
		}
	}

	public void CloseTicket()
	{
		Manager.PlayClick(); // ЗВУК КЛІКУ
		if ( OpenedTicket != null && OpenedTicket.IsScratched )
		{
			TicketsOnTable.Remove( OpenedTicket );
		}
		OpenedTicket = null;
		StateHasChanged();
	}

	private async Task AutoclickerLoop()
	{
		while ( AutoclickerLevel > 0 && IsValid )
		{
			// Автоклікеру звук ми не даємо, інакше він буде "спамити" і дратувати
			decimal amount = 0.01m * (1m + IncomeLevel);
			if ( HasVipPass ) amount *= 10m;
			Money += amount;

			StateHasChanged();
			float delay = Math.Max( 0.1f, 0.4f * (float)Math.Pow( 0.9, AutoclickerLevel - 1 ) );
			await GameTask.DelaySeconds( delay );
		}
	}

	private async Task VacuumLoop()
	{
		while ( HasVacuum && IsValid )
		{
			float speed = 0.05f + (0.02f * VacuumSpeedLevel);

			foreach ( var tick in TicketsOnTable.Where( t => t.ProcessState == 0 && !t.IsScratched && draggedTicket != t ) )
			{
				tick.X += (120f + (System.Random.Shared.NextSingle() * 10f - 5f) - tick.X) * speed;
				tick.Y += (350f + (System.Random.Shared.NextSingle() * 10f - 5f) - tick.Y) * speed;

				if ( Math.Abs( tick.X - 120f ) < 40 && Math.Abs( tick.Y - 350f ) < 40 )
				{
					tick.ProcessState = 1;
					tick.Rotation = System.Random.Shared.NextSingle() * 20f - 10f;
				}
			}
			StateHasChanged();
			await GameTask.Delay( 50 );
		}
	}

	private async Task AutoScratcherLoop()
	{
		while ( HasAutoScratcher && IsValid )
		{
			float delay = Math.Max( 0.2f, 2.0f * (float)Math.Pow( 0.8, ScratcherSpeedLevel ) );

			var topTicket = TicketsOnTable.FirstOrDefault( t => t.ProcessState == 1 && draggedTicket != t );
			if ( topTicket != null )
			{
				topTicket.ProcessState = 2;
				topTicket.ZIndex = 5;
				topTicket.X = 120f;
				topTicket.Rotation = 0f;

				int steps = 15;
				float slideDelay = delay / steps;

				for ( int i = 0; i < steps; i++ )
				{
					if ( topTicket == null || !IsValid ) break;
					topTicket.Y += (240f) / steps;
					topTicket.IsBeingProcessed = true;
					StateHasChanged();
					await GameTask.DelaySeconds( slideDelay );
				}

				if ( topTicket != null && IsValid && TicketsOnTable.Contains( topTicket ) )
				{
					topTicket.ProcessState = 3;
					topTicket.IsScratched = true;
					topTicket.IsBeingProcessed = false;
					topTicket.ZIndex = 10;
					topTicket.Y = 600f + (System.Random.Shared.NextSingle() * 20f - 10f);
					topTicket.X = 120f + (System.Random.Shared.NextSingle() * 20f - 10f);
					topTicket.Rotation = System.Random.Shared.NextSingle() * 40f - 20f;
					StateHasChanged();
				}
			}
			else
			{
				await GameTask.Delay( 100 );
			}
		}
	}

	private async Task AutoOpenerLoop()
	{
		while ( HasAutoOpener && IsValid )
		{
			float delay = Math.Max( 0.1f, 1.0f * (float)Math.Pow( 0.8, OpenerSpeedLevel ) );

			var scratched = TicketsOnTable.FirstOrDefault( t => t.IsScratched && !t.IsBeingProcessed && draggedTicket != t );
			if ( scratched != null )
			{
				ClaimPrize( scratched );
			}

			await GameTask.DelaySeconds( delay );
		}
	}

	public void OnTicketScratched( decimal winAmount )
	{
		Money += winAmount;

		TicketHistory.Add( new HistoryEntry
		{
			Amount = winAmount,
			Message = winAmount > 0 ? $"Win: ${winAmount.ToString( "0.00" )}" : "You lost..."
		} );

		_ = DestroyTicketAfterDelay();
	}

	private async Task DestroyTicketAfterDelay()
	{
		try
		{
			await GameTask.DelaySeconds( 2.0f );
			if ( !IsValid ) return;

			if ( OpenedTicket != null )
			{
				TicketsOnTable.Remove( OpenedTicket );
				OpenedTicket = null;
			}
			StateHasChanged();
		}
		catch ( System.Exception )
		{
		}
	}

	protected override int BuildHash() => System.HashCode.Combine( Money, TicketsOnTable.Count, OpenedTicket != null, TicketHistory.Count, TopPlayersBoard?.Entries?.Length ?? 0 );
}
