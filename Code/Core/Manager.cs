using Sandbox;

/// <summary>
/// Главный системный компонент проекта, отвечающий за глобальные настройки игры и звук.
/// Этот класс вешается на объекты в сцене S&box.
/// </summary>
public sealed class Manager : Component
{
	// Синглтон, чтобы мы могли вызывать звуки из любого другого скрипта
	public static Manager Instance { get; private set; }

	// Настройки путей к звукам (можно менять в инспекторе S&box)
	[Property, Group( "Audio" )] public string BgmSoundPath { get; set; } = "sounds/bgm.sound";
	[Property, Group( "Audio" )] public string ClickSoundPath { get; set; } = "sounds/click.sound";
	[Property, Group( "Audio" )] public string ScratchSoundPath { get; set; } = "sounds/scratch.sound";

	// Хэндл (ссылка) на фоновую музыку, чтобы она не терялась
	private SoundHandle _bgmHandle;

	protected override void OnAwake()
	{
		// Присваиваем этот экземпляр в статичную переменную
		Instance = this;
	}

	protected override void OnStart()
	{
		try
		{
			// Включаем фоновую музыку при старте сцены
			if ( !string.IsNullOrWhiteSpace( BgmSoundPath ) )
			{
				_bgmHandle = Sound.Play( BgmSoundPath );

				// ЗАХИСТ ВІД КРАШУ: Перевіряємо, чи звук дійсно завантажився, перш ніж міняти гучність
				if ( _bgmHandle != null && _bgmHandle.IsValid )
				{
					// Делаем музыку тише, чтобы она не перебивала эффекты (0.3f = 30% громкости)
					_bgmHandle.Volume = 0.3f;
				}
				else
				{
					Log.Warning( $"Не вдалося знайти фонову музику за шляхом: {BgmSoundPath}" );
				}
			}
		}
		catch ( System.Exception ex )
		{
			Log.Warning( $"Помилка при запуску фонової музики: {ex.Message}" );
		}
	}

	protected override void OnUpdate()
	{
		// Устаревший API S&box: Включает видимость курсора мыши на экране.
		Mouse.Visible = true;
	}

	/// <summary>
	/// Глобальный метод: Воспроизводит звук клика.
	/// </summary>
	public static void PlayClick()
	{
		try
		{
			if ( Instance != null && !string.IsNullOrWhiteSpace( Instance.ClickSoundPath ) )
			{
				Sound.Play( Instance.ClickSoundPath );
			}
		}
		catch ( System.Exception )
		{
			// Ігноруємо помилку, якщо звуку кліку немає
		}
	}

	/// <summary>
	/// Глобальный метод: Возвращает путь к звуку стирания монетки
	/// </summary>
	public static string GetScratchSoundPath()
	{
		return Instance != null ? Instance.ScratchSoundPath : "sounds/scratch.sound";
	}
}
