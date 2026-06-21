using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using System;

namespace Damascus.Mapper.Controls
{
	sealed class ViewportOpenGL : OpenGlControlBase
	{


		protected override void OnOpenGlInit(GlInterface gl)
		{
			base.OnOpenGlInit(gl);
		}

		protected override void OnOpenGlDeinit(GlInterface gl)
		{
			base.OnOpenGlDeinit(gl);
		}

		protected override void OnOpenGlLost()
		{
			base.OnOpenGlLost();
		}

		protected override void OnOpenGlRender(GlInterface gl, int fb)
		{

		}
	}
}
