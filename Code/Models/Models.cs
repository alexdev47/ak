using System;

namespace Sandbox;

/// <summary>
/// Структура для збереження прогресу гравця
/// </summary>
public class GameSaveData
{
	public decimal Money { get; set; }
	public int IncomeLevel { get; set; }
	public int AutoclickerLevel { get; set; }
	public int ScratcherSpeedLevel { get; set; }
	public int OpenerSpeedLevel { get; set; }
	public int VacuumSpeedLevel { get; set; }

	public bool HasAutoclicker { get; set; }
	public bool HasAutoScratcher { get; set; }
	public bool HasAutoOpener { get; set; }
	public bool HasVacuum { get; set; }
	public bool HasVipPass { get; set; }
}

/// <summary>
/// Представляет активный лотерейный билет, который сейчас находится на столе.
/// </summary>
public class ActiveTicket
{
	// Уникальный идентификатор билета
	public Guid Id { get; set; } = Guid.NewGuid();

	// Позиция билета на столе (координаты по X и Y)
	public float X { get; set; }
	public float Y { get; set; }

	// Угол поворота билета (в градусах)
	public float Rotation { get; set; }

	// Приоритет отрисовки (какой билет лежит поверх других)
	public int ZIndex { get; set; }

	// Сумма выигрыша в этом билете. Если 0, то билет проигрышный.
	public decimal Prize { get; set; }

	// Категория билета (Бронза, Серебро, Золото и т.д.)
	public string Tier { get; set; }

	// Физические размеры билета на столе
	public float Width { get; set; }
	public float Height { get; set; }

	// Статус: был ли уже стерт (сыгран) этот билет
	public bool IsScratched { get; set; } = false;

	// Статус: обрабатывается ли билет прямо сейчас авто-скретчером (машинкой)
	public bool IsBeingProcessed { get; set; } = false;

	// Состояние билета в конвейере авто-машинки:
	// 0 = Лежит на столе (ждет или куплен)
	// 1 = Засосан пылесосом и находится над машинкой
	// 2 = Внутри машинки (едет вниз и стирается)
	// 3 = Выпал снизу машинки (стертый)
	public int ProcessState { get; set; } = 0;

	// Сгенерированные символы и номера
	public List<string> Symbols { get; set; } = new();
	public List<string> TopNumbers { get; set; } = new();
}

/// <summary>
/// Запись в истории покупок и выигрышей.
/// </summary>
public class HistoryEntry
{
	// Текстовое описание события (например, "Куплен билет" или "Выигрыш")
	public string Message { get; set; }

	// Сумма изменения баланса (положительная при выигрыше, отрицательная при покупке)
	public decimal Amount { get; set; }
}

/// <summary>
/// Всплывающий текст с цифрами (урон, доход, выигрыш), появляющийся на экране.
/// </summary>
public class FloatingText
{
	// Координаты на экране, где появится текст
	public int X { get; set; }
	public int Y { get; set; }

	// Текст (обычно "+$10.00" или "Loss")
	public string Text { get; set; }

	// Цвет текста (красный для потерь, зеленый для дохода)
	public string Color { get; set; }
}
