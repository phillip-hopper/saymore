// ---------------------------------------------------------------------------------------------
#region // Copyright (c) 2010, SIL International. All Rights Reserved.
// <copyright from='2010' to='2010' company='SIL International'>
//		Copyright (c) 2010, SIL International. All Rights Reserved.
//
//		Distributable under the terms of either the Common Public License or the
//		GNU Lesser General Public License, as specified in the LICENSING.txt file.
// </copyright>
#endregion
//
// File: SpongeBar.cs
// Responsibility: D. Olson
//
// <remarks>
// </remarks>
// ---------------------------------------------------------------------------------------------
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SilUtils;

namespace SIL.Sponge.Controls
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Implements a tool strip control used for making pretty blue gradient tool bars in
	/// the application.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class SpongeBar : ToolStrip
	{
		public static Color DefaultSpongeBarColorBegin =
			ColorHelper.CalculateColor(Color.LightSteelBlue, Color.White, 200);

		public static Color DefaultSpongeBarColorEnd = Color.SteelBlue;

		private ToolStripRenderer m_prevRenderer;
		private Color m_clrBegin = DefaultSpongeBarColorBegin;
		private Color m_clrEnd = DefaultSpongeBarColorEnd;

		#region Properties
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets or sets the gradient angle.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public float GradientAngle { get; set; }

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the lighter color of the gradient sponge bar color.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public Color BackColorBegin
		{
			get { return m_clrBegin; }
			set { m_clrBegin = value; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the darker color of the gradient sponge bar color.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public Color BackColorEnd
		{
			get { return m_clrEnd; }
			set { m_clrEnd = value; }
		}

		#endregion

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Raises the <see cref="E:System.Windows.Forms.ToolStrip.RendererChanged"/> event.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected override void OnRendererChanged(EventArgs e)
		{
			if (m_prevRenderer != null)
				m_prevRenderer.RenderToolStripBorder -= OverrideSpongeBarBorderPainting;

			base.OnRendererChanged(e);

			Renderer.RenderToolStripBorder += OverrideSpongeBarBorderPainting;
			m_prevRenderer = Renderer;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Paint the background of the toolbar our own way.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected override void OnPaintBackground(PaintEventArgs e)
		{
			base.OnPaintBackground(e);
			PaintSpongeBarBackground(e.Graphics, ClientRectangle);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Paint over the default toolstrip border with the same gradient used to paint the
		/// background of the rest of the main toolstrip.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void OverrideSpongeBarBorderPainting(object sender, ToolStripRenderEventArgs e)
		{
			// Paint over the bottom 3 pixels of the toolbar.
			Rectangle rc = e.ToolStrip.ClientRectangle;
			rc.Y = rc.Bottom - 3;
			PaintSpongeBarBackground(e.Graphics, rc);

			// Paint over a couple of pixels at the left edge.
			rc = e.ToolStrip.ClientRectangle;
			rc.Width = 2;
			using (var br = new SolidBrush(BackColorBegin))
				e.Graphics.FillRectangle(br, rc);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Paint in the specified rectangle the gradient blue of a sponge bar background.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void PaintSpongeBarBackground(Graphics g, Rectangle rc)
		{
			using (var br = new LinearGradientBrush(rc, BackColorBegin, BackColorEnd, GradientAngle))
				g.FillRectangle(br, rc);
		}
	}
}