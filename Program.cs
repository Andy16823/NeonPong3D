using LibGFX.Graphics.Renderer.OpenGL;

namespace NeonPong3D;

internal static class Program
{
    [STAThread]
    private static void Main() =>  new PongGame().Run(new GLRenderer(), 1280, 800, "NEON PONG // GFX", false);
}
