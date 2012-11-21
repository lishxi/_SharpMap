// Copyright 2005, 2006 - Morten Nielsen (www.iter.dk)
//
// This file is part of SharpMap.
// SharpMap is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// SharpMap is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with SharpMap; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA 

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using GeoAPI.Geometries;
using SharpMap.Rendering.Exceptions;
using SharpMap.Web.Wms;
using Common.Logging;

namespace SharpMap.Layers
{
    /// <summary>
    /// Web Map Service layer
    /// </summary>
    /// <remarks>
    /// The WmsLayer is currently very basic and doesn't support automatic fetching of the WMS Service Description.
    /// Instead you would have to add the nessesary parameters to the URL,
    /// and the WmsLayer will set the remaining BoundingBox property and proper requests that changes between the requests.
    /// See the example below.
    /// </remarks>
    /// <example>
    /// The following example creates a map with a WMS layer the Demis WMS Server
    /// <code lang="C#">
    /// myMap = new SharpMap.Map(new System.Drawing.Size(500,250);
    /// string wmsUrl = "http://www2.demis.nl/mapserver/request.asp";
    /// SharpMap.Layers.WmsLayer myLayer = new SharpMap.Layers.WmsLayer("Demis WMS", myLayer);
    /// myLayer.AddLayer("Bathymetry");
    /// myLayer.AddLayer("Countries");
    /// myLayer.AddLayer("Topography");
    /// myLayer.AddLayer("Hillshading");
    /// myLayer.SetImageFormat(layWms.OutputFormats[0]);
    /// myLayer.SRID = 4326;	
    /// myMap.Layers.Add(myLayer);
    /// myMap.Center = new SharpMap.Geometries.Point(0, 0);
    /// myMap.Zoom = 360;
    /// myMap.MaximumZoom = 360;
    /// myMap.MinimumZoom = 0.1;
    /// </code>
    /// </example>
    public class WmsLayer : Layer
    {

        private static readonly ILog logger = LogManager.GetLogger(typeof(WmsLayer));


        private Boolean _ContinueOnError;
        private ICredentials _Credentials;
        private ImageAttributes _ImageAttributes;
        private Collection<string> _LayerList;
        private string _MimeType = "";
        private IWebProxy _Proxy;
        private Collection<string> _StylesList;
        private int _TimeOut;
        private Client wmsClient;
        private bool _Transparancy = true;
        private Color _BgColor = Color.White;
        private string _capabilitiesUrl;

        /// <summary>
        /// Initializes a new layer, and downloads and parses the service description
        /// </summary>
        /// <remarks>In and ASP.NET application the service description is automatically cached for 24 hours when not specified</remarks>
        /// <param name="layername">Layername</param>
        /// <param name="url">Url of WMS server</param>
        public WmsLayer(string layername, string url)
            : this(layername, url, new TimeSpan(24, 0, 0))
        {
        }

        /// <summary>
        /// Initializes a new layer, and downloads and parses the service description
        /// </summary>
        /// <param name="layername">Layername</param>
        /// <param name="url">Url of WMS server</param>
        /// <param name="cachetime">Time for caching Service Description (ASP.NET only)</param>
        public WmsLayer(string layername, string url, TimeSpan cachetime)
            : this(layername, url, cachetime, null)
        {
        }

        /// <summary>
        /// Initializes a new layer, and downloads and parses the service description
        /// </summary>
        /// <remarks>In and ASP.NET application the service description is automatically cached for 24 hours when not specified</remarks>
        /// <param name="layername">Layername</param>
        /// <param name="url">Url of WMS server</param>
        /// <param name="proxy">Proxy</param>
        public WmsLayer(string layername, string url, IWebProxy proxy)
            : this(layername, url, new TimeSpan(24, 0, 0), proxy)
        {
        }

        /// <summary>
        /// Initializes a new layer, and downloads and parses the service description
        /// </summary>
        /// <param name="layername">Layername</param>
        /// <param name="url">Url of WMS server</param>
        /// <param name="cachetime">Time for caching Service Description (ASP.NET only)</param>
        /// <param name="proxy">Proxy</param>
        public WmsLayer(string layername, string url, TimeSpan cachetime, IWebProxy proxy)
            : this(layername, url, new TimeSpan(24, 0, 0), proxy, null)
        {
        }

        /// <summary>
        /// Initializes a new layer, and downloads and parses the service description
        /// </summary>
        /// <param name="layername">Layername</param>
        /// <param name="url">Url of WMS server</param>
        /// <param name="cachetime">Time for caching Service Description (ASP.NET only)</param>
        /// <param name="proxy">Proxy</param>
        /// <param name="credentials"></param>
        public WmsLayer(string layername, string url, TimeSpan cachetime, IWebProxy proxy, ICredentials credentials)
        {
            _capabilitiesUrl = url;
            _Proxy = proxy;
            _TimeOut = 10000;
            LayerName = layername;
            _ContinueOnError = true;
            _Credentials = credentials;

            if (!Web.HttpCacheUtility.TryGetValue("SharpMap_WmsClient_" + url, out wmsClient))
            {
                if (logger.IsDebugEnabled)
                    logger.Debug("Creating new client for url " + url);
                wmsClient = new Client(url, _Proxy, _Credentials);

                if (!Web.HttpCacheUtility.TryAddValue("SharpMap_WmsClient_" + url, wmsClient))
                {
                    if (logger.IsDebugEnabled)
                        logger.Debug("Adding client to Cache for url " + url + " failed");
                }
            }
            else
            {
                if (logger.IsDebugEnabled)
                    logger.Debug("Created client from Cache for url " + url);
            }
            /*
            if (HttpContext.Current != null && HttpContext.Current.Cache["SharpMap_WmsClient_" + url] != null)
            {
                if (logger.IsDebugEnabled)
                    logger.Debug("Creating client from Cache for url " + url);

                wmsClient = (Client)HttpContext.Current.Cache["SharpMap_WmsClient_" + url];
            }
            else
            {
                if (logger.IsDebugEnabled)
                    logger.Debug("Creating new client for url " + url);
                wmsClient = new Client(url, _Proxy, _Credentials);
                if (HttpContext.Current != null)
                    HttpContext.Current.Cache.Insert("SharpMap_WmsClient_" + url, wmsClient, null,
                                                     Cache.NoAbsoluteExpiration, cachetime);
            }
             */
            //Set default mimetype - We prefer compressed formats
            if (OutputFormats.Contains("image/jpeg")) _MimeType = "image/jpeg";
            else if (OutputFormats.Contains("image/png")) _MimeType = "image/png";
            else if (OutputFormats.Contains("image/gif")) _MimeType = "image/gif";
            else //None of the default formats supported - Look for the first supported output format
            {
                bool formatSupported = false;
                foreach (ImageCodecInfo encoder in ImageCodecInfo.GetImageEncoders())
                    if (OutputFormats.Contains(encoder.MimeType.ToLower()))
                    {
                        formatSupported = true;
                        _MimeType = encoder.MimeType;
                        break;
                    }
                if (!formatSupported)
                    throw new ArgumentException(
                        "GDI+ doesn't not support any of the mimetypes supported by this WMS service");
            }
            _LayerList = new Collection<string>();
            _StylesList = new Collection<string>();
        }

        /// <summary>
        /// Can be used to force the OnlineResourceUrl for services that return incorrect (often internal) onlineresources
        /// </summary>
        /// <param name="url">Url without any OGC specific parameters</param>
        public void ForceOnlineResourceUrl(string url)
        {
            for (int i = 0; i < wmsClient.GetMapRequests.Length; i++)
            {
                wmsClient.GetMapRequests[i].OnlineResource = url;
            }        
        }


        /// <summary>
        /// Gets the list of enabled layers
        /// </summary>
        public Collection<string> LayerList
        {
            get { return _LayerList; }
        }

        /// <summary>
        /// Gets the list of enabled styles
        /// </summary>
        public Collection<string> StylesList
        {
            get { return _StylesList; }
        }

        /// <summary>
        /// Gets the hiarchial list of available WMS layers from this service
        /// </summary>
        public Client.WmsServerLayer RootLayer
        {
            get { return wmsClient.Layer; }
        }

        /// <summary>
        /// Gets the list of available formats
        /// </summary>
        public Collection<string> OutputFormats
        {
            get { return wmsClient.GetMapOutputFormats; }
        }

        /// <summary>
        /// Sets the optional transparancy. The WMS server might ignore this when not implemented and will ignore if the imageformat is jpg
        /// </summary>
        public bool Transparancy
        {
            get { return _Transparancy; }
            set { _Transparancy = value; }
        }

        /// <summary>
        /// Set the opacity on the drawn image, this method updates the ImageAttributes with opacity-values and is used when sharpmap draws the image, the the wms-server
        /// 1.0 = No tranparency
        /// 0.0 = full transparency
        /// </summary>
        /// <param name="opacity"></param>
        public void SetOpacity(float opacity)
        {
            ColorMatrix cmxPic = new ColorMatrix();
            cmxPic.Matrix33 = opacity;
            ImageAttributes attrs = ImageAttributes;
            if (attrs == null)
                attrs = new System.Drawing.Imaging.ImageAttributes();
            attrs.SetColorMatrix(cmxPic, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            ImageAttributes = attrs;
        }

        /// <summary>
        /// Sets the optional backgroundcolor. 
        /// </summary>
        public Color BgColor
        {
            get { return _BgColor; }
            set { _BgColor = value; }

        }


        /// <summary>
        /// Gets the service description from this server
        /// </summary>
        public Capabilities.WmsServiceDescription ServiceDescription
        {
            get { return wmsClient.ServiceDescription; }
        }

        /// <summary>
        /// Gets or sets the WMS Server version of this service
        /// </summary>
        public string Version
        {
            get { return wmsClient.Version; }
            set { wmsClient.Version = value; }
        }

        /// <summary>
        /// Gets a value indicating the URL for the 'GetCapablities' request
        /// </summary>
        public string CapabilitiesUrl
        {
            get { return _capabilitiesUrl; }
        }


        /// <summary>
        /// When specified, applies image attributes at image (fx. make WMS layer semi-transparent)
        /// </summary>
        /// <remarks>
        /// <para>You can make the WMS layer semi-transparent by settings a up a ColorMatrix,
        /// or scale/translate the colors in any other way you like.</para>
        /// <example>
        /// Setting the WMS layer to be semi-transparent.
        /// <code lang="C#">
        /// float[][] colorMatrixElements = { 
        ///				new float[] {1,  0,  0,  0, 0},
        ///				new float[] {0,  1,  0,  0, 0},
        ///				new float[] {0,  0,  1,  0, 0},
        ///				new float[] {0,  0,  0,  0.5, 0},
        ///				new float[] {0, 0, 0, 0, 1}};
        /// ColorMatrix colorMatrix = new ColorMatrix(colorMatrixElements);
        /// ImageAttributes imageAttributes = new ImageAttributes();
        /// imageAttributes.SetColorMatrix(
        /// 	   colorMatrix,
        /// 	   ColorMatrixFlag.Default,
        /// 	   ColorAdjustType.Bitmap);
        /// myWmsLayer.ImageAttributes = imageAttributes;
        /// </code>
        /// </example>
        /// </remarks>
        public ImageAttributes ImageAttributes
        {
            get { return _ImageAttributes; }
            set { _ImageAttributes = value; }
        }

        /// <summary>
        /// Returns the extent of the layer
        /// </summary>
        /// <returns>Bounding box corresponding to the extent of the features in the layer</returns>
        public override Envelope Envelope
        {
            //There are two boundingboxes available: 1 as EPSG:4326 with LatLonCoordinates and or one with the coordinates of the FIRST BoundingBox with SRID of the layer
            //If the request is using EPSG:4326, it should use the boundingbox with LatLon coordinates, else, it should use the boundingbox with the coordinates in the SRID
            get
            {
                var boxes = new Collection<Envelope>();
                var sridBoxes = getBoundingBoxes(RootLayer);
                foreach (var sridBox in sridBoxes)
                {
                    if (SRID == sridBox.SRID)
                        boxes.Add(sridBox);
                }

                if (boxes.Count > 0)
                {
                    var res = new Envelope();
                    foreach (var envelope in boxes)
                        res.ExpandToInclude(envelope);

                    return res;
                }

                if (SRID == 4326)
                    return RootLayer.LatLonBoundingBox;

                //There is no boundingbox defined. Maybe we should throw a NotImplementedException
                //TODO: project one of the available bboxes to layers projection
                return null;
            }
        }

        /// <summary>
        /// Specifies whether to throw an exception if the Wms request failed, or to just skip rendering the layer
        /// </summary>
        public Boolean ContinueOnError
        {
            get { return _ContinueOnError; }
            set { _ContinueOnError = value; }
        }

        /// <summary>
        /// Provides the base authentication interface for retrieving credentials for Web client authentication.
        /// </summary>
        public ICredentials Credentials
        {
            get { return _Credentials; }
            set { _Credentials = value; }
        }

        /// <summary>
        /// Gets or sets the proxy used for requesting a webresource
        /// </summary>
        public IWebProxy Proxy
        {
            get { return _Proxy; }
            set { _Proxy = value; }
        }

        /// <summary>
        /// Timeout of webrequest in milliseconds. Defaults to 10 seconds
        /// </summary>
        public int TimeOut
        {
            get { return _TimeOut; }
            set { _TimeOut = value; }
        }

        /// <summary>
        /// Adds a layer to WMS request
        /// </summary>
        /// <remarks>Layer names are case sensitive.</remarks>
        /// <param name="name">Name of layer</param>
        /// <exception cref="System.ArgumentException">Throws an exception is an unknown layer is added</exception>
        public void AddLayer(string name)
        {
            if (!LayerExists(wmsClient.Layer, name))
                throw new ArgumentException("Cannot add WMS Layer - Unknown layername");

            _LayerList.Add(name);
        }

        /// <summary>
        /// Recursive method for checking whether a layername exists
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private bool LayerExists(Client.WmsServerLayer layer, string name)
        {
            if (name == layer.Name) return true;
            foreach (Client.WmsServerLayer childlayer in layer.ChildLayers)
                if (LayerExists(childlayer, name)) return true;
            return false;
        }

        /// <summary>
        /// Removes a layer from the layer list
        /// </summary>
        /// <param name="name">Name of layer to remove</param>
        public void RemoveLayer(string name)
        {
            _LayerList.Remove(name);
        }

        /// <summary>
        /// Removes the layer at the specified index
        /// </summary>
        /// <param name="index"></param>
        public void RemoveLayerAt(int index)
        {
            _LayerList.RemoveAt(index);
        }

        /// <summary>
        /// Removes all layers
        /// </summary>
        public void RemoveAllLayers()
        {
            _LayerList.Clear();
        }

        /// <summary>
        /// Adds a style to the style collection
        /// </summary>
        /// <param name="name">Name of style</param>
        /// <exception cref="System.ArgumentException">Throws an exception is an unknown layer is added</exception>
        public void AddStyle(string name)
        {
            if (!StyleExists(wmsClient.Layer, name))
                throw new ArgumentException("Cannot add WMS Layer - Unknown layername");
            _StylesList.Add(name);
        }

        /// <summary>
        /// Recursive method for checking whether a layername exists
        /// </summary>
        /// <param name="layer">layer</param>
        /// <param name="name">name of style</param>
        /// <returns>True of style exists</returns>
        private bool StyleExists(Client.WmsServerLayer layer, string name)
        {
            foreach (Client.WmsLayerStyle style in layer.Style)
                if (name == style.Name) return true;
            foreach (Client.WmsServerLayer childlayer in layer.ChildLayers)
                if (StyleExists(childlayer, name)) return true;
            return false;
        }

        /// <summary>
        /// Removes a style from the collection
        /// </summary>
        /// <param name="name">Name of style</param>
        public void RemoveStyle(string name)
        {
            _StylesList.Remove(name);
        }

        /// <summary>
        /// Removes a style at specified index
        /// </summary>
        /// <param name="index">Index</param>
        public void RemoveStyleAt(int index)
        {
            _StylesList.RemoveAt(index);
        }

        /// <summary>
        /// Removes all styles from the list
        /// </summary>
        public void RemoveAllStyles()
        {
            _StylesList.Clear();
        }

        /// <summary>
        /// Sets the image type to use when requesting images from the WMS server
        /// </summary>
        /// <remarks>
        /// <para>See the <see cref="OutputFormats"/> property for a list of available mime types supported by the WMS server</para>
        /// </remarks>
        /// <exception cref="ArgumentException">Throws an exception if either the mime type isn't offered by the WMS
        /// or GDI+ doesn't support this mime type.</exception>
        /// <param name="mimeType">Mime type of image format</param>
        public void SetImageFormat(string mimeType)
        {
            if (!OutputFormats.Contains(mimeType))
                throw new ArgumentException("WMS service doesn't not offer mimetype '" + mimeType + "'");
            //Check whether SharpMap supports the specified mimetype
            bool formatSupported = false;
            foreach (ImageCodecInfo encoder in ImageCodecInfo.GetImageEncoders())
                if (encoder.MimeType.ToLower() == mimeType.ToLower())
                {
                    formatSupported = true;
                    break;
                }
            if (!formatSupported)
                throw new ArgumentException("GDI+ doesn't not support mimetype '" + mimeType + "'");
            _MimeType = mimeType;
        }

        /// <summary>
        /// Renders the layer
        /// </summary>
        /// <param name="g">Graphics object reference</param>
        /// <param name="map">Map which is rendered</param>
        public override void Render(Graphics g, Map map)
        {
            if (logger.IsDebugEnabled)
                logger.Debug("Rendering wmslayer: " + this.LayerName);

            Client.WmsOnlineResource resource = GetPreferredMethod();
            Uri myUri = new Uri(GetRequestUrl(map.Envelope, map.Size));

            if (logger.IsDebugEnabled)
                logger.Debug("Url: " + myUri.ToString());

            WebRequest myWebRequest = WebRequest.Create(myUri);
            myWebRequest.Method = resource.Type;
            myWebRequest.Timeout = _TimeOut;

            if (myWebRequest is HttpWebRequest)
            {
                (myWebRequest as HttpWebRequest).Accept = _MimeType;
                (myWebRequest as HttpWebRequest).KeepAlive = false;
                (myWebRequest as HttpWebRequest).UserAgent = "SharpMap-WMSLayer";
            }

            if (_Credentials != null)
            {
                myWebRequest.Credentials = _Credentials;
                myWebRequest.PreAuthenticate = true;
            }
            else
                myWebRequest.Credentials = CredentialCache.DefaultCredentials;

            if (_Proxy != null)
                myWebRequest.Proxy = _Proxy;

            try
            {
                if (logger.IsDebugEnabled)
                    logger.Debug("Beginning request");

                HttpWebResponse myWebResponse = (HttpWebResponse)myWebRequest.GetResponse();

                if (logger.IsDebugEnabled)
                    logger.Debug("Got response");

                if (myWebResponse != null)
                {
                    Stream dataStream = myWebResponse.GetResponseStream();

                    if (dataStream != null && myWebResponse.ContentType.StartsWith("image"))
                    {
                        if (logger.IsDebugEnabled)
                            logger.Debug("Reading image from stream");

                        Image img = null;
                        int cLength = (int)myWebResponse.ContentLength;

                        if (logger.IsDebugEnabled)
                            logger.Debug("Content-Length: " + cLength);

                        using (MemoryStream ms = new MemoryStream())
                        {
                            byte[] buf = new byte[50000];
                            int numRead = 0;
                            DateTime lastTimeGotData = DateTime.Now;
                            bool moreToRead = true;
                            do
                            {
                                try
                                {
                                    int nr = dataStream.Read(buf, 0, buf.Length);
                                    ms.Write(buf, 0, nr);
                                    numRead += nr;

                                    if (nr == 0)
                                    {
                                        int testByte = dataStream.ReadByte();
                                        if (testByte == -1)
                                        {
                                            moreToRead = false;
                                            break;
                                        }
                                        else
                                        {
                                            if (((TimeSpan)(DateTime.Now - lastTimeGotData)).TotalSeconds > _TimeOut)
                                            {
                                                if (logger.IsInfoEnabled)
                                                    logger.Info("Did not get any data for " + _TimeOut + " seconds, aborting");
                                                return;

                                            }

                                            if (logger.IsDebugEnabled)
                                                logger.Debug("No data to read. Have received: " + numRead + " of " + cLength);


                                            //Did not get data... sleep for a while to not spin
                                            System.Threading.Thread.Sleep(10);
                                        }
                                    }
                                    else
                                    {
                                        lastTimeGotData = DateTime.Now;
                                    }

                                }
                                catch (IOException /*ee*/)
                                {
                                    //This can be valid since in some cases .NET failed to parse 0-sized chunks in responses..
                                    //For now, just safely ignore the exception and assume we read all data...
                                    //Either way we will get an error later if we did not..
                                    moreToRead = false;
                                }
                                catch (Exception ee)
                                {
                                    logger.Error("Error reading from WMS-server..", ee);
                                    throw;
                                }

                            }
                            while (moreToRead);

                            if (logger.IsDebugEnabled)
                                logger.Debug("Have received: " + numRead);


                            ms.Seek(0, SeekOrigin.Begin);
                            img = Image.FromStream(ms);
                        }


                        if (logger.IsDebugEnabled)
                            logger.Debug("Image read.. Drawing");

                        if (_ImageAttributes != null)
                            g.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height), 0, 0,
                                        img.Width, img.Height, GraphicsUnit.Pixel, ImageAttributes);
                        else
                            g.DrawImageUnscaled(img, 0, 0, map.Size.Width, map.Size.Height);

                        if (img != null)
                            img.Dispose();

                        if (logger.IsDebugEnabled)
                            logger.Debug("Draw complete");

                        dataStream.Close();
                    }
                    myWebResponse.Close();
                }
            }
            catch (WebException webEx)
            {
                if (!_ContinueOnError)
                    throw (new RenderException(
                        "There was a problem connecting to the WMS server when rendering layer '" + LayerName + "'",
                        webEx));
                logger.Error("There was a problem connecting to the WMS server when rendering layer '" + LayerName +
                            "'", webEx);
            }
            catch (Exception ex)
            {
                if (!_ContinueOnError)
                    throw (new RenderException("There was a problem rendering layer '" + LayerName + "'", ex));
                logger.Error("There was a problem connecting to the WMS server when rendering layer '" + LayerName +
                            "'", ex);
            }
            base.Render(g, map);
        }

        /// <summary>
        /// Gets the URL for a map request base on current settings, the image size and boundingbox
        /// </summary>
        /// <param name="box">Area the WMS request should cover</param>
        /// <param name="size">Size of image</param>
        /// <returns>URL for WMS request</returns>
        public string GetRequestUrl(Envelope box, Size size)
        {
            Client.WmsOnlineResource resource = GetPreferredMethod();
            StringBuilder strReq = new StringBuilder(resource.OnlineResource);
            if (!resource.OnlineResource.Contains("?"))
                strReq.Append("?");
            if (!strReq.ToString().EndsWith("&") && !strReq.ToString().EndsWith("?"))
                strReq.Append("&");

            strReq.AppendFormat(Map.NumberFormatEnUs, "REQUEST=GetMap&BBOX={0},{1},{2},{3}",
                                box.MinX, box.MinY, box.MaxX, box.MaxY);
            strReq.AppendFormat("&WIDTH={0}&Height={1}", size.Width, size.Height);
            strReq.Append("&Layers=");
            if (_LayerList != null && _LayerList.Count > 0)
            {
                foreach (string layer in _LayerList)
                    strReq.AppendFormat("{0},", layer);
                strReq.Remove(strReq.Length - 1, 1);
            }
            strReq.AppendFormat("&FORMAT={0}", _MimeType);
            if (SRID < 0)
                throw new ApplicationException("Spatial reference system not set");
            if (wmsClient.Version == "1.3.0")
                strReq.AppendFormat("&CRS=EPSG:{0}", SRID);
            else
                strReq.AppendFormat("&SRS=EPSG:{0}", SRID);
            strReq.AppendFormat("&VERSION={0}", wmsClient.Version);
            strReq.Append("&Styles=");
            if (_StylesList != null && _StylesList.Count > 0)
            {
                foreach (string style in _StylesList)
                    strReq.AppendFormat("{0},", style);
                strReq.Remove(strReq.Length - 1, 1);
            }
            strReq.AppendFormat("&TRANSPARENT={0}", _Transparancy);
            if (!_Transparancy)
                strReq.AppendFormat("&BGCOLOR={0}", ColorTranslator.ToHtml(_BgColor));
            return strReq.ToString();
        }

        /// <summary>
        /// Returns the preferred URL to use when communicating with the wms-server
        /// Favors GET-requests over POST-requests
        /// </summary>
        /// <returns>Instance of Client.WmsOnlineResource</returns>
        protected Client.WmsOnlineResource GetPreferredMethod()
        {
            //We prefer get. Seek for supported 'get' method
            for (int i = 0; i < wmsClient.GetMapRequests.Length; i++)
                if (wmsClient.GetMapRequests[i].Type.ToLower() == "get")
                    return wmsClient.GetMapRequests[i];
            //Next we prefer the 'post' method
            for (int i = 0; i < wmsClient.GetMapRequests.Length; i++)
                if (wmsClient.GetMapRequests[i].Type.ToLower() == "post")
                    return wmsClient.GetMapRequests[i];
            return wmsClient.GetMapRequests[0];
        }

        /// <summary>
        /// Gets all the boundingboxes from the Client.WmsServerLayer
        /// </summary>
        /// <returns>List of all spatial referenced boundingboxes</returns>
        private List<SpatialReferencedBoundingBox> getBoundingBoxes(Client.WmsServerLayer layer)
        {
            List<SpatialReferencedBoundingBox> box = new List<SpatialReferencedBoundingBox>();
            box.AddRange(layer.SRIDBoundingBoxes);
            if (layer.ChildLayers.Length > 0)
            {
                for (int i = 0; i < layer.ChildLayers.Length; i++)
                {
                    box.AddRange(getBoundingBoxes(layer.ChildLayers[i]));
                }
            }
            return box;
        }

        /// <summary>
        /// Recursive method for adding all WMS layers to layer list
        /// Skips "top level" layer if addFirstLayer is false
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="addFirstLayer"></param>
        /// <returns></returns>
        public void AddChildLayers(Client.WmsServerLayer layer, bool addFirstLayer)
        {
            if (addFirstLayer)
                this.AddLayer(layer.Name);
            else
                addFirstLayer = true;


            foreach (Client.WmsServerLayer childlayer in layer.ChildLayers)
            {
                AddChildLayers(childlayer, addFirstLayer);
            }
        }
    }
}