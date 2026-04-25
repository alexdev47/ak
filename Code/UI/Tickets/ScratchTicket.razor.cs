using Sandbox;
using Sandbox.UI;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Lottery;

/// <summary>
/// Логика компонента "Стираемый лотерейный билет".
/// Отвечает за генерацию символов, физику стирания мышкой и проверку победы.
/// </summary>
public partial class ScratchTicket : Panel
{
	public class Cell
	{
		public float X;
		public float Y;
		public float CenterX;
		public float CenterY;
		public bool IsScratched;
	}

	public class TicketLayoutConfig
	{
		public string BgImage { get; set; }
		public int TicketW { get; set; }
		public int TicketH { get; set; }
		public int SymX { get; set; }
		public int SymY { get; set; }
		public int SymW { get; set; }
		public int SymH { get; set; }
		public int SymGap { get; set; }
		public List<(int x, int y, int w, int h)> ScratchZones { get; set; }
	}

	public List<Cell> Cells { get; set; } = new();
	[Property] public string TicketTier { get; set; } = "БРОНЗА";
	[Property] public decimal PrizeAmount { get; set; } = 0m;
	[Property] public Action<decimal> OnComplete { get; set; }
	[Property] public bool IsPreScratched { get; set; } = false;
	[Property] public List<string> Symbols { get; set; } = new();
	[Property] public List<string> TopNumbers { get; set; } = new();

	TicketLayoutConfig Config;
	bool isFinished = false;

	public const int CellSize = 6;
	const float ScratchBrushRadius = 20f;
	const float CursorOffsetX = 0f;
	const float CursorOffsetY = 0f;

	// Ссылка на звук стирания, чтобы мы могли его выключать, когда мышка отпущена
	private SoundHandle _scratchSoundHandle;

	protected override void OnParametersSet()
	{
		base.OnParametersSet();

		if ( TicketTier == "СРІБЛО" )
		{
			Config = new TicketLayoutConfig
			{
				BgImage = "/ui/tickets/2.png",
				TicketW = 600,
				TicketH = 450,
				SymX = 60,
				SymY = 285,
				SymW = 480,
				SymH = 87,
				SymGap = 50,
				ScratchZones = new() { (52, 288, 139, 81), (228, 288, 139, 81), (404, 288, 139, 81) }
			};
		}
		else if ( TicketTier == "ЗОЛОТО" )
		{
			Config = new TicketLayoutConfig
			{
				BgImage = "/ui/tickets/3.png",
				TicketW = 300,
				TicketH = 500,
				SymX = 69,
				SymY = 134,
				SymW = 180,
				SymH = 85,
				SymGap = 5,
				ScratchZones = new() { (52, 240, 49, 45), (123, 240, 49, 45), (198, 240, 49, 45),
										(52, 319, 49, 45),(123, 319, 49, 45),(198, 319, 49, 45),
										(52, 399, 49, 45),(123, 399, 49, 45),(198, 399, 49, 45)}
			};
		}
		else if ( TicketTier == "ВИП" )
		{
			Config = new TicketLayoutConfig
			{
				BgImage = "/ui/tickets/3.png",
				TicketW = 300,
				TicketH = 500,
				SymX = 69,
				SymY = 134,
				SymW = 180,
				SymH = 85,
				SymGap = 5,
				ScratchZones = new() { (52, 240, 49, 45), (123, 240, 49, 45), (198, 240, 49, 45),
										(52, 319, 49, 45),(123, 319, 49, 45),(198, 319, 49, 45),
										(52, 399, 49, 45),(123, 399, 49, 45),(198, 399, 49, 45)}
			};
		}
		else // БРОНЗА
		{
			Config = new TicketLayoutConfig
			{
				BgImage = "/ui/tickets/1.png",
				TicketW = 380,
				TicketH = 520,
				SymX = 56,
				SymY = 350,
				SymW = 259,
				SymH = 86,
				SymGap = 5,
				ScratchZones = new() { (55, 349, 260, 85) }
			};
		}

		if ( Cells.Count == 0 && Config != null )
		{
			foreach ( var zone in Config.ScratchZones )
			{
				for ( int r = zone.y; r < zone.y + zone.h; r += CellSize )
				{
					for ( int c = zone.x; c < zone.x + zone.w; c += CellSize )
					{
						Cells.Add( new Cell { X = c, Y = r, CenterX = c + CellSize / 2f, CenterY = r + CellSize / 2f, IsScratched = false } );
					}
				}
			}

			GenerateSymbols();

			if ( IsPreScratched )
			{
				isFinished = true;
				foreach ( var cell in Cells ) cell.IsScratched = true;
				OnComplete?.Invoke( PrizeAmount );
			}
		}
	}

	bool isScratching = false;

	// --- Обработчики мыши для стирания ---

	void OnMouseDown()
	{
		if ( isFinished ) return;
		isScratching = true;
		ProcessScratch( (Vector2)MousePosition );
	}

	void OnMouseUp()
	{
		isScratching = false;
		StopScratchSound(); // Останавливаем звук, когда отпустили мышку
	}

	void OnMouseMove()
	{
		if ( isScratching && !isFinished )
		{
			ProcessScratch( (Vector2)MousePosition );
		}
	}

	void OnMouseLeave()
	{
		isScratching = false;
		StopScratchSound(); // Вывели за пределы билета — звук стоп
	}

	// Вспомогательный метод остановки звука
	void StopScratchSound()
	{
		if ( _scratchSoundHandle != null && _scratchSoundHandle.IsPlaying )
		{
			_scratchSoundHandle.Stop();
			_scratchSoundHandle = null;
		}
	}

	void GenerateSymbols()
	{
		if ( Symbols.Count > 0 || TopNumbers.Count > 0 ) return; // Вже згенеровано в Main.razor.cs

		Symbols.Clear();
		TopNumbers.Clear();

		if ( TicketTier == "ЗОЛОТО" )
		{
			int matchesNeeded = 0;
			if ( PrizeAmount > 0m )
			{
				if ( PrizeAmount == 50m ) matchesNeeded = 1;
				else if ( PrizeAmount == 450m ) matchesNeeded = 2;
				else if ( PrizeAmount == 1350m ) matchesNeeded = 3;
				else if ( PrizeAmount == 4050m ) matchesNeeded = 4;
			}

			List<int> topNums = new();
			while ( topNums.Count < 4 )
			{
				int r = System.Random.Shared.Next( 1, 101 );
				if ( !topNums.Contains( r ) ) topNums.Add( r );
			}
			TopNumbers = topNums.Select( n => n.ToString() ).ToList();

			List<int> bottomNums = new();
			List<int> matchesToPlace = topNums.Take( matchesNeeded ).ToList();

			foreach ( var match in matchesToPlace )
			{
				bottomNums.Add( match );
			}

			while ( bottomNums.Count < 9 )
			{
				int r = System.Random.Shared.Next( 1, 101 );
				if ( !topNums.Contains( r ) && !bottomNums.Contains( r ) ) bottomNums.Add( r );
			}

			bottomNums = bottomNums.OrderBy( x => System.Random.Shared.NextSingle() ).ToList();
			Symbols = bottomNums.Select( n => n.ToString() ).ToList();
			return;
		}

		string[] possible = TicketTier == "СРІБЛО" ? new string[] {
			"/ui/tickets/symbols/brainrot1.png", "/ui/tickets/symbols/brainrot2.png", "/ui/tickets/symbols/brainrot3.png",
			"/ui/tickets/symbols/brainrot4.png", "/ui/tickets/symbols/brainrot5.png", "/ui/tickets/symbols/brainrot6.png"
		} : new string[] {
			"/ui/tickets/symbols/Action!.png", "/ui/tickets/symbols/chest.png", "/ui/tickets/symbols/energy.png",
			"/ui/tickets/symbols/gift.png", "/ui/tickets/symbols/message.png",
			"/ui/tickets/symbols/skull.png"
		};

		string braintor6 = "/ui/tickets/symbols/brainrot6.png";

		if ( PrizeAmount > 0m )
		{
			if ( TicketTier == "СРІБЛО" )
			{
				int brainrotCount = 0;
				if ( PrizeAmount >= 500m ) brainrotCount = 3;
				else if ( PrizeAmount >= 100m ) brainrotCount = 2;
				else brainrotCount = 1;

				for ( int i = 0; i < brainrotCount; i++ )
				{
					Symbols.Add( braintor6 );
				}

				while ( Symbols.Count < 3 )
				{
					string rndS = possible[System.Random.Shared.Next( 0, possible.Length )];
					if ( rndS != braintor6 )
					{
						Symbols.Add( rndS );
					}
				}
				Symbols = Symbols.OrderBy( x => System.Random.Shared.NextSingle() ).ToList();
			}
			else if ( TicketTier == "БРОНЗА" )
			{
				string winSymbol = "/ui/tickets/symbols/chest.png";
				if ( PrizeAmount == 3m ) winSymbol = "/ui/tickets/symbols/energy.png";
				else if ( PrizeAmount == 5m ) winSymbol = "/ui/tickets/symbols/message.png";
				else if ( PrizeAmount == 10m ) winSymbol = "/ui/tickets/symbols/skull.png";
				else if ( PrizeAmount == 15m ) winSymbol = "/ui/tickets/symbols/gift.png";
				else if ( PrizeAmount == 20m ) winSymbol = "/ui/tickets/symbols/Action!.png";

				for ( int i = 0; i < 3; i++ ) Symbols.Add( winSymbol );
			}
			else
			{
				string winSymbol = possible[System.Random.Shared.Next( 0, possible.Length )];
				for ( int i = 0; i < 3; i++ ) Symbols.Add( winSymbol );
			}
		}
		else
		{
			Dictionary<string, int> counts = new();
			while ( Symbols.Count < 3 )
			{
				string rndS = possible[System.Random.Shared.Next( 0, possible.Length )];
				if ( TicketTier == "СРІБЛО" && rndS == braintor6 ) continue;

				counts.TryGetValue( rndS, out int c );
				if ( c < 2 )
				{
					counts[rndS] = c + 1;
					Symbols.Add( rndS );
				}
			}
			Symbols = Symbols.OrderBy( x => System.Random.Shared.NextSingle() ).ToList();
		}
	}

	void ProcessScratch( Vector2 pos )
	{
		float targetX = pos.x + CursorOffsetX;
		float targetY = pos.y + CursorOffsetY;
		float rSq = ScratchBrushRadius * ScratchBrushRadius;
		bool changed = false;

		foreach ( var c in Cells )
		{
			if ( c.IsScratched ) continue;

			float dx = c.CenterX - targetX;
			float dy = c.CenterY - targetY;
			if ( dx * dx + dy * dy < rSq )
			{
				c.IsScratched = true;
				changed = true;
			}
		}

		if ( changed )
		{
			// --- ЗВУК СТИРАНИЯ ---
			// Если мы стерли новый пиксель, и звук еще не играет — включаем его
			if ( _scratchSoundHandle == null || !_scratchSoundHandle.IsPlaying )
			{
				_scratchSoundHandle = Sound.Play( Manager.GetScratchSoundPath() );
			}

			int scratchedCount = Cells.Count( c => c.IsScratched );
			if ( scratchedCount > Cells.Count * 0.90f )
			{
				isFinished = true;

				// Билет стерт — моментально останавливаем звук!
				StopScratchSound();

				foreach ( var c in Cells ) c.IsScratched = true;
				OnComplete?.Invoke( PrizeAmount );
			}
			StateHasChanged();
		}
	}

	protected override int BuildHash() => System.HashCode.Combine( Cells?.Count( c => c.IsScratched ) ?? 0, isFinished, PrizeAmount );
}
