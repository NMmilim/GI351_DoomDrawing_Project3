/// <summary>
/// Global switch for the Big Boss "sandbox" test bed.
///
/// When enabled:
/// - Earth is indestructible: asteroid impacts neither drain resources nor trigger Game Over.
/// - AsteroidSpawner launches the mass snail-pace swarm (20-30 meteors per wave).
///
/// The inspector checkbox lives on AsteroidSpawner (it sets this in Awake and when edited at runtime);
/// Player_controller and Rock only read it. Turn the checkbox off to get the normal game back.
/// </summary>
public static class SandboxMode
{
    public static bool Enabled { get; set; }
}
