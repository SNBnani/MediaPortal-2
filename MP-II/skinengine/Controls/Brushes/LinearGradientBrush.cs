#region Copyright (C) 2007 Team MediaPortal

/*
    Copyright (C) 2007 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal II

    MediaPortal II is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal II is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using MediaPortal.Core.Properties;
using SkinEngine.Effects;
using SkinEngine.DirectX;
using SkinEngine.Controls.Visuals;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using SkinEngine;

namespace SkinEngine.Controls.Brushes
{
  public class LinearGradientBrush : GradientBrush, IAsset
  {
    Texture _texture;
    double _height;
    double _width;
    EffectAsset _effect;
    DateTime _lastTimeUsed;

    Property _startPointProperty;
    Property _endPointProperty;
    float[] _offsets = new float[6];
    ColorValue[] _colors = new ColorValue[6];
    bool _refresh = false;
    bool _singleColor = true;
    PositionColored2Textured[] _verts;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinearGradientBrush"/> class.
    /// </summary>
    public LinearGradientBrush()
    {
      Init();
    }
    public LinearGradientBrush(LinearGradientBrush b)
      : base(b)
    {
      Init();
      StartPoint = b.StartPoint;
      EndPoint = b.EndPoint;
    }
    void Init()
    {
      _startPointProperty = new Property(new Vector2(0.0f, 0.0f));
      _endPointProperty = new Property(new Vector2(1.0f, 1.0f));
      ContentManager.Add(this);
      _startPointProperty.Attach(new PropertyChangedHandler(OnPropertyChanged));
      _endPointProperty.Attach(new PropertyChangedHandler(OnPropertyChanged));
    }

    public override object Clone()
    {
      return new LinearGradientBrush(this);
    }

    /// <summary>
    /// Gets or sets the start point property.
    /// </summary>
    /// <value>The start point property.</value>
    public Property StartPointProperty
    {
      get
      {
        return _startPointProperty;
      }
      set
      {
        _startPointProperty = value;
      }
    }

    /// <summary>
    /// Gets or sets the start point.
    /// </summary>
    /// <value>The start point.</value>
    public Vector2 StartPoint
    {
      get
      {
        return (Vector2)_startPointProperty.GetValue();
      }
      set
      {
        _startPointProperty.SetValue(value);
      }
    }
    /// <summary>
    /// Gets or sets the end point property.
    /// </summary>
    /// <value>The end point property.</value>
    public Property EndPointProperty
    {
      get
      {
        return _endPointProperty;
      }
      set
      {
        _endPointProperty = value;
      }
    }

    /// <summary>
    /// Gets or sets the end point.
    /// </summary>
    /// <value>The end point.</value>
    public Vector2 EndPoint
    {
      get
      {
        return (Vector2)_endPointProperty.GetValue();
      }
      set
      {
        _endPointProperty.SetValue(value);
      }
    }

    /// <summary>
    /// Called when a property changed.
    /// </summary>
    /// <param name="prop">The prop.</param>
    protected override void OnPropertyChanged(Property prop)
    {
      _refresh = true;
    }
    /// <summary>
    /// Setups the brush.
    /// </summary>
    /// <param name="element">The element.</param>
    public override void SetupBrush(FrameworkElement element, ref PositionColored2Textured[] verts)
    {
      Trace.WriteLine("LinearGradientBrush.SetupBrush()");
      _verts = verts;
      // if (_texture == null || element.ActualHeight != _height || element.ActualWidth != _width)
      {
        if (!IsOpacityBrush)
          base.SetupBrush(element, ref verts);

        _height = element.ActualHeight;
        _width = element.ActualWidth;
        if (_texture == null)
        {
          _texture = new Texture(GraphicsDevice.Device, 256, 2, 1, Usage.None, Format.X8R8G8B8, Pool.Managed);
        }
        _refresh = true;
      }
    }
    void CreateGradient()
    {
      int[,] buffer = (int[,])_texture.LockRectangle(typeof(int), 0, LockFlags.None, new int[] { (int)2, (int)256 });
      float width = 256.0f;
      for (int i = 0; i < GradientStops.Count - 1; ++i)
      {
        GradientStop stopbegin = GradientStops[i];
        GradientStop stopend = GradientStops[i + 1];
        ColorValue colorStart = ColorValue.FromColor(stopbegin.Color);
        ColorValue colorEnd = ColorValue.FromColor(stopend.Color);
        int offsetStart = (int)(stopbegin.Offset * width);
        int offsetEnd = (int)(stopend.Offset * width);

        float distance = offsetEnd - offsetStart;
        for (int x = offsetStart; x < offsetEnd; ++x)
        {
          float step = (x - offsetStart) / distance;
          float r = step * (colorEnd.Red - colorStart.Red);
          r += colorStart.Red;

          float g = step * (colorEnd.Green - colorStart.Green);
          g += colorStart.Green;

          float b = step * (colorEnd.Blue - colorStart.Blue);
          b += colorStart.Blue;

          float a = step * (colorEnd.Alpha - colorStart.Alpha);
          a += colorStart.Alpha;

          ColorValue color = new ColorValue(r, g, b, a);
          if (IsOpacityBrush)
            color = new ColorValue(a, a, a, 1);
          buffer[0, x] = color.ToArgb();
          buffer[1, x] = color.ToArgb();
        }
      }

      _texture.UnlockRectangle(0);
      //TextureLoader.Save(@"c:\1\gradient.png",ImageFileFormat.Png,_texture);
    }

    void SetColor(VertexBuffer vertexbuffer)
    {
      ColorValue color = ColorValue.FromColor(GradientStops[0].Color);
      color.Alpha *= (float)Opacity;
      for (int i = 0; i < _verts.Length; ++i)
      {
        _verts[i].Color = color.ToArgb();
      }
      vertexbuffer.SetData(_verts, 0, LockFlags.None);
    }

    /// <summary>
    /// Begins the render.
    /// </summary>
    public override void BeginRender(VertexBuffer vertexBuffer, int primitiveCount, PrimitiveType primitiveType)
    {
      if (_texture == null)
      {
        return;
      }
      if (_refresh)
      {
        _refresh = false;
        int index = 0;
        foreach (GradientStop stop in GradientStops)
        {
          _offsets[index] = (float)stop.Offset;
          _colors[index] = ColorValue.FromColor(stop.Color);
          _colors[index].Alpha *= (float)Opacity;
          index++;
        }
        _singleColor = true;
        for (int i = 0; i < GradientStops.Count - 1; ++i)
        {
          if (_colors[i].ToArgb() != _colors[i + 1].ToArgb())
          {
            _singleColor = false;
            break;
          }
        }
        CreateGradient();
        if (_singleColor)
        {
          SetColor(vertexBuffer);
        }
      }

      GraphicsDevice.Device.Transform.World = SkinContext.FinalMatrix.Matrix;
      if (!_singleColor)
      {
        if (IsOpacityBrush)
        {
          _effect = ContentManager.GetEffect("linearopacitygradient");
        }
        else
        {
          _effect = ContentManager.GetEffect("lineargradient");
        }
        //_effect.Parameters["g_offset"] = _offsets;
        //_effect.Parameters["g_color"] = _colors;
        //_effect.Parameters["g_stops"] = (int)GradientStops.Count;
        _effect.Parameters["g_opacity"] = (float)Opacity;
        _effect.Parameters["g_StartPoint"] = new float[2] { StartPoint.X, StartPoint.Y };
        _effect.Parameters["g_EndPoint"] = new float[2] { EndPoint.X, EndPoint.Y };
        Matrix m = Matrix.Identity;
        RelativeTransform.GetTransform(out m);
        m = Matrix.Invert(m);
        _effect.Parameters["RelativeTransform"] = m;

        _effect.StartRender(_texture);
        _lastTimeUsed = SkinContext.Now;
      }
      else
      {
        GraphicsDevice.Device.SetTexture(0, null);
        _lastTimeUsed = SkinContext.Now;
      }
    }

    public override void BeginRender(Texture tex)
    {
      if (tex == null)
      {
        return;
      }
      if (_refresh)
      {
        _refresh = false;
        int index = 0;
        foreach (GradientStop stop in GradientStops)
        {
          _offsets[index] = (float)stop.Offset;
          _colors[index] = ColorValue.FromColor(stop.Color);
          _colors[index].Alpha *= (float)Opacity;
          index++;
        }
        _singleColor = true;
        for (int i = 0; i < GradientStops.Count - 1; ++i)
        {
          if (_colors[i].ToArgb() != _colors[i + 1].ToArgb())
          {
            _singleColor = false;
            break;
          }
        }
        CreateGradient();
        if (_singleColor)
        {
          //SetColor(vertexBuffer);
        }
      }

      GraphicsDevice.Device.Transform.World = SkinContext.FinalMatrix.Matrix;
      if (!_singleColor)
      {
        if (IsOpacityBrush)
        {
          _effect = ContentManager.GetEffect("linearopacitygradient");
        }
        else
        {
          _effect = ContentManager.GetEffect("lineargradient");
        }
        _effect.Parameters["g_alphatex"] = _texture;
        _effect.Parameters["g_StartPoint"] = new float[2] { StartPoint.X, StartPoint.Y };
        _effect.Parameters["g_EndPoint"] = new float[2] { EndPoint.X, EndPoint.Y };
        Matrix m = Matrix.Identity;
        RelativeTransform.GetTransform(out m);
        m = Matrix.Invert(m);
        _effect.Parameters["RelativeTransform"] = m;

        _effect.StartRender(tex);
        _lastTimeUsed = SkinContext.Now;
      }
      else
      {
        GraphicsDevice.Device.SetTexture(0, null);
        _lastTimeUsed = SkinContext.Now;
      }
    }

    /// <summary>
    /// Ends the render.
    /// </summary>
    public override void EndRender()
    {
      if (_effect != null)
      {
        _effect.EndRender();
        _effect = null;
      }
    }

    #region IAsset Members

    /// <summary>
    /// Gets a value indicating the asset is allocated
    /// </summary>
    /// <value><c>true</c> if this asset is allocated; otherwise, <c>false</c>.</value>
    public bool IsAllocated
    {
      get
      {
        return (_texture != null);
      }
    }

    /// <summary>
    /// Gets a value indicating whether this asset can be deleted.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this asset can be deleted; otherwise, <c>false</c>.
    /// </value>
    public bool CanBeDeleted
    {
      get
      {
        if (!IsAllocated)
        {
          return false;
        }
        TimeSpan ts = SkinContext.Now - _lastTimeUsed;
        if (ts.TotalSeconds >= 1)
        {
          return true;
        }

        return false;
      }
    }

    /// <summary>
    /// Frees this asset.
    /// </summary>
    public void Free()
    {
      if (_texture != null)
      {
        _texture.Dispose();
        _texture = null;
      }
    }

    #endregion

    public override Texture Texture
    {
      get
      {
        return _texture;
      }
    }
  }
}
