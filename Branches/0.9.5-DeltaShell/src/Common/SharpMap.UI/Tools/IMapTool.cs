using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GeoAPI.Extensions.Feature;
using GeoAPI.Geometries;
using SharpMap.Api;
using SharpMap.Layers;
using SharpMap.UI.Forms;
using System.ComponentModel;
using DelftTools.Utils.Collections;

namespace SharpMap.UI.Tools
{
    public interface IMapTool // TODO: IMapControlTool ?
    {
        IMapControl MapControl { get; set; }

        void OnMouseDown(ICoordinate worldPosition, MouseEventArgs e);
        
        void OnMouseMove(ICoordinate worldPosition, MouseEventArgs e);
        
        void OnMouseUp(ICoordinate worldPosition, MouseEventArgs e);

        void OnMouseWheel(ICoordinate worldPosition, MouseEventArgs e);

        void OnMouseDoubleClick(object sender, MouseEventArgs e);

        void OnMouseHover(ICoordinate worldPosition, EventArgs e);

        void OnKeyDown(KeyEventArgs e);

        void OnKeyUp(KeyEventArgs e);

        /// <summary>
        /// TODO: remove, Render is probably enough
        /// </summary>
        /// <param name="e"></param>
        void OnPaint(PaintEventArgs e);

        /// <summary>
        /// Renders tool graphics for a given mapBox.
        /// </summary>
        /// <param name="graphics"></param>
        /// <param name="mapBox"></param>
        void Render(Graphics graphics, Map mapBox);

        /// <summary>
        /// Indicates whether what the tool renders in world or screen coordinates.
        /// When tool renders in screen coordinates - results of render are unaffected by dragging, resize, etc.
        /// Otherwise results of rendering are related to world coordinates (e.g. specific features, like for selections, tooltips)
        /// </summary>
        bool RendersInScreenCoordinates { get; }

        void OnMapLayerRendered(Graphics g, ILayer layer);

        void OnMapPropertyChanged(object sender, PropertyChangedEventArgs e);

        void OnMapCollectionChanged(object sender, NotifyCollectionChangingEventArgs e);

        void OnBeforeContextMenu(ContextMenuStrip menu, ICoordinate worldPosition);

        void OnDragEnter(DragEventArgs e);

        void OnDragDrop(DragEventArgs e);

        /// <summary>
        /// Returns true if tool is currently busy (working).
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// True when tool is currently active (can be used).
        /// </summary>
        bool IsActive { get; set; }

        /// <summary>
        /// True when tool is currently enabled.
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// Returns true if tool is always active, e.g. mouse wheel zoom,. fixed zoom in/out, zoom to map extent ...
        /// 
        /// <remarks>If tool is AlwaysActive - Execute() method should be used and not ActivateTool().</remarks>
        /// </summary>
        bool AlwaysActive { get; }

        /// <summary>
        /// Used for AlwaysActive tools.
        /// </summary>
        void Execute();

        /// <summary>
        /// Cancels the current operation. Map should revert to state before start tool
        /// </summary>
        void Cancel();

        /// <summary>
        /// User readable name of tool.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Map tool may be applied only to a set of layers. This property allows to define a filter for these layers. 
        /// Then the layers can be obtained using <see cref="Layers"/> property.
        /// </summary>
        Func<ILayer, bool> LayerFilter { get; set; }

        /// <summary>
        /// Returns layers which satisfy <see cref="LayerFilter"/>.
        /// </summary>
        IEnumerable<ILayer> Layers { get; }

        void ActiveToolChanged(IMapTool newTool); // TODO: remove, why tool should know about changes of active tool

        //ILayer GetLayerByGeometry(IGeometry geometry);
        ILayer GetLayerByFeature(IFeature feature);
    }
}