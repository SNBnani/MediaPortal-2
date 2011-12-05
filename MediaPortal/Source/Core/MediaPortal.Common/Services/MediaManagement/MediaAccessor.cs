#region Copyright (C) 2007-2011 Team MediaPortal

/*
    Copyright (C) 2007-2011 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.PluginManager;
using MediaPortal.Common.Services.ResourceAccess;
using MediaPortal.Common.Services.ResourceAccess.LocalFsResourceProvider;
using MediaPortal.Common.Services.ResourceAccess.RemoteResourceProvider;
using MediaPortal.Common.SystemResolver;
using MediaPortal.Utilities.SystemAPI;

namespace MediaPortal.Common.Services.MediaManagement
{
  public delegate void NotifyResourceProviderChangeDelegate(IResourceProvider resourceProvider);

  public delegate void NotifyMetadataExtractorChangeDelegate(IMetadataExtractor metadataExtractor);

  /// <summary>
  /// This is the base class for client and server media managers.
  /// It contains the functionality to load resource providers and metadata extractors.
  /// </summary>
  public class MediaAccessor : IMediaAccessor
  {
    #region Constants

    // Localization resources will be provided by the client's SkinBase plugin
    public const string MY_MUSIC_SHARE_NAME_RESOURE = "[Media.MyMusic]";
    public const string MY_VIDEOS_SHARE_NAME_RESOURCE = "[Media.MyVideos]";
    public const string MY_PICTURES_SHARE_NAME_RESOURCE = "[Media.MyPictures]";

    // Constants will be moved to some constants class
    protected const string RESOURCE_PROVIDERS_PLUGIN_LOCATION = "/ResourceProviders";
    protected const string METADATA_EXTRACTORS_PLUGIN_LOCATION = "/Media/MetadataExtractors";

    protected const string METADATA_EXTRACTORS_USE_COMPONENT_NAME = "MediaAccessor: MetadataExtractors";
    protected const string RESOURCE_PROVIDERS_USE_COMPONENT_NAME = "MediaAccessor: ResourceProviders";

    #endregion

    #region Internal classes

    protected class MetadataExtractorPluginItemChangeListener : IItemRegistrationChangeListener
    {
      protected MediaAccessor _parent;

      internal MetadataExtractorPluginItemChangeListener(MediaAccessor parent)
      {
        _parent = parent;
      }

      public void ItemsWereAdded(string location, ICollection<PluginItemMetadata> items)
      {
        IPluginManager pluginManager = ServiceRegistration.Get<IPluginManager>();
        foreach (PluginItemMetadata item in items)
        {
          IMetadataExtractor metadataExtractor = pluginManager.RequestPluginItem<IMetadataExtractor>(
              item.RegistrationLocation, item.Id, new FixedItemStateTracker(METADATA_EXTRACTORS_USE_COMPONENT_NAME));
          _parent.RegisterMetadataExtractor(metadataExtractor);
        }
      }

      public void ItemsWereRemoved(string location, ICollection<PluginItemMetadata> items)
      {
        // TODO: Make MetadataExtractors removable?
      }
    }

    protected class ResourceProviderPluginItemChangeListener : IItemRegistrationChangeListener
    {
      protected MediaAccessor _parent;

      internal ResourceProviderPluginItemChangeListener(MediaAccessor parent)
      {
        _parent = parent;
      }

      public void ItemsWereAdded(string location, ICollection<PluginItemMetadata> items)
      {
        IPluginManager pluginManager = ServiceRegistration.Get<IPluginManager>();
        foreach (PluginItemMetadata item in items)
        {
          IResourceProvider resourceProvider = pluginManager.RequestPluginItem<IResourceProvider>(
              item.RegistrationLocation, item.Id, new FixedItemStateTracker(RESOURCE_PROVIDERS_USE_COMPONENT_NAME));
          _parent.RegisterProvider(resourceProvider);
        }
      }

      public void ItemsWereRemoved(string location, ICollection<PluginItemMetadata> items)
      {
        // TODO: Make ResourceProviders removable?
      }
    }

    #endregion

    #region Protected fields

    protected object _syncObj = new object();
    protected ResourceProviderPluginItemChangeListener _resourceProvidersPluginItemChangeListener;
    protected MetadataExtractorPluginItemChangeListener _metadataExtractorsPluginItemChangeListener;
    protected IDictionary<Guid, IResourceProvider> _providers = null;
    protected IDictionary<Guid, IMetadataExtractor> _metadataExtractors = null;

    #endregion

    #region Ctor

    public MediaAccessor()
    {
      _resourceProvidersPluginItemChangeListener = new ResourceProviderPluginItemChangeListener(this);
      _metadataExtractorsPluginItemChangeListener = new MetadataExtractorPluginItemChangeListener(this);
    }

    #endregion

    #region Protected methods

    protected void RegisterProvider(IResourceProvider provider)
    {
      lock (_syncObj)
        _providers.Add(provider.Metadata.ResourceProviderId, provider);
      MediaAccessorMessaging.SendResourceProviderMessage(MediaAccessorMessaging.MessageType.ResourceProviderAdded, provider.Metadata.ResourceProviderId);
    }

    protected void RegisterMetadataExtractor(IMetadataExtractor metadataExtractor)
    {
      lock (_syncObj)
        _metadataExtractors.Add(metadataExtractor.Metadata.MetadataExtractorId, metadataExtractor);
      MediaAccessorMessaging.SendResourceProviderMessage(MediaAccessorMessaging.MessageType.MetadataExtractorAdded, metadataExtractor.Metadata.MetadataExtractorId);
    }

    protected void RegisterCoreProviders()
    {
      RegisterProvider(new LocalFsResourceProvider());
      RegisterProvider(new RemoteResourceProvider());
    }

    protected void DisposeProviders()
    {
      if (_providers == null)
        return;
      foreach (IDisposable d in _providers.Values.OfType<IDisposable>())
        d.Dispose();
      _providers = null;
    }

    protected void DisposeMetadataExtractors()
    {
      if (_metadataExtractors == null)
        return;
      foreach (IDisposable d in _metadataExtractors.Values.OfType<IDisposable>())
        d.Dispose();
      _metadataExtractors = null;
    }

    /// <summary>
    /// Checks that the provider plugins are loaded.
    /// </summary>
    protected void CheckProvidersLoaded()
    {
      lock (_syncObj)
      {
        if (_providers != null)
          return;
        _providers = new Dictionary<Guid, IResourceProvider>();
      }
      foreach (IResourceProvider provider in ServiceRegistration.Get<IPluginManager>().RequestAllPluginItems<IResourceProvider>(
          RESOURCE_PROVIDERS_PLUGIN_LOCATION, new FixedItemStateTracker(RESOURCE_PROVIDERS_USE_COMPONENT_NAME)))
        RegisterProvider(provider);
      RegisterCoreProviders();
    }

    /// <summary>
    /// Checks that the provider plugins are loaded.
    /// </summary>
    protected void CheckMetadataExtractorsLoaded()
    {
      lock (_syncObj)
      {
        if (_metadataExtractors != null)
          return;
        _metadataExtractors = new Dictionary<Guid, IMetadataExtractor>();
      }
      foreach (IMetadataExtractor metadataExtractor in ServiceRegistration.Get<IPluginManager>().RequestAllPluginItems<IMetadataExtractor>(
          METADATA_EXTRACTORS_PLUGIN_LOCATION, new FixedItemStateTracker(METADATA_EXTRACTORS_USE_COMPONENT_NAME))) // TODO: Make metadata extractors removable
        RegisterMetadataExtractor(metadataExtractor);
    }

    protected void RegisterPluginItemListeners()
    {
      IPluginManager pluginManager = ServiceRegistration.Get<IPluginManager>();
      pluginManager.AddItemRegistrationChangeListener(RESOURCE_PROVIDERS_PLUGIN_LOCATION,
          _resourceProvidersPluginItemChangeListener);
      pluginManager.AddItemRegistrationChangeListener(METADATA_EXTRACTORS_PLUGIN_LOCATION,
          _metadataExtractorsPluginItemChangeListener);
    }

    protected void UnregisterPluginItemListeners()
    {
      IPluginManager pluginManager = ServiceRegistration.Get<IPluginManager>();
      pluginManager.RemoveItemRegistrationChangeListener(RESOURCE_PROVIDERS_PLUGIN_LOCATION,
          _resourceProvidersPluginItemChangeListener);
      pluginManager.RemoveItemRegistrationChangeListener(METADATA_EXTRACTORS_PLUGIN_LOCATION,
          _metadataExtractorsPluginItemChangeListener);
    }

    #endregion

    #region IMediaAccessor implementation

    public IDictionary<Guid, IResourceProvider> LocalResourceProviders
    {
      get
      {
        CheckProvidersLoaded();
        lock (_syncObj)
          return new Dictionary<Guid, IResourceProvider>(_providers);
      }
    }

    public IEnumerable<IBaseResourceProvider> LocalBaseResourceProviders
    {
      get { return LocalResourceProviders.Values.OfType<IBaseResourceProvider>(); }
    }

    public IEnumerable<IChainedResourceProvider> LocalChainedResourceProviders
    {
      get { return LocalResourceProviders.Values.OfType<IChainedResourceProvider>(); }
    }

    public IDictionary<Guid, IMetadataExtractor> LocalMetadataExtractors
    {
      get
      {
        CheckMetadataExtractorsLoaded();
        lock (_syncObj)
          return new Dictionary<Guid, IMetadataExtractor>(_metadataExtractors);
      }
    }

    public virtual void Initialize()
    {
      RegisterPluginItemListeners();
    }

    public virtual void Shutdown()
    {
      UnregisterPluginItemListeners();
      DisposeProviders();
      DisposeMetadataExtractors();
    }

    public ICollection<Share> CreateDefaultShares()
    {
      List<Share> result = new List<Share>();
      Guid localFsResourceProviderId = LocalFsResourceProviderBase.LOCAL_FS_RESOURCE_PROVIDER_ID;
      if (LocalResourceProviders.ContainsKey(localFsResourceProviderId))
      {
        string folderPath;
        if (WindowsAPI.GetSpecialFolder(WindowsAPI.SpecialFolder.MyMusic, out folderPath))
        {
          folderPath = LocalFsResourceProviderBase.ToProviderPath(folderPath);
          string[] mediaCategories = new[] {DefaultMediaCategory.Audio.ToString()};
          Share sd = Share.CreateNewLocalShare(ResourcePath.BuildBaseProviderPath(localFsResourceProviderId, folderPath),
              MY_MUSIC_SHARE_NAME_RESOURE, mediaCategories);
          result.Add(sd);
        }

        if (WindowsAPI.GetSpecialFolder(WindowsAPI.SpecialFolder.MyVideos, out folderPath))
        {
          folderPath = LocalFsResourceProviderBase.ToProviderPath(folderPath);
          string[] mediaCategories = new[] { DefaultMediaCategory.Video.ToString() };
          Share sd = Share.CreateNewLocalShare(ResourcePath.BuildBaseProviderPath(localFsResourceProviderId, folderPath),
              MY_VIDEOS_SHARE_NAME_RESOURCE, mediaCategories);
          result.Add(sd);
        }

        if (WindowsAPI.GetSpecialFolder(WindowsAPI.SpecialFolder.MyPictures, out folderPath))
        {
          folderPath = LocalFsResourceProviderBase.ToProviderPath(folderPath);
          string[] mediaCategories = new[] { DefaultMediaCategory.Image.ToString() };
          Share sd = Share.CreateNewLocalShare(ResourcePath.BuildBaseProviderPath(localFsResourceProviderId, folderPath),
              MY_PICTURES_SHARE_NAME_RESOURCE, mediaCategories);
          result.Add(sd);
        }
      }
      if (result.Count > 0)
        return result;
      // Fallback: If no share was added for the defaults above, use the provider's root folders
      result.AddRange(LocalBaseResourceProviders.Select(resourceProvider => resourceProvider.Metadata).Select(
          metadata => Share.CreateNewLocalShare(ResourcePath.BuildBaseProviderPath(metadata.ResourceProviderId, "/"), metadata.Name, null)));
      return result;
    }

    public IResourceLocator GetResourceLocator(MediaItem item)
    {
      if (item == null || !item.Aspects.ContainsKey(ProviderResourceAspect.ASPECT_ID))
        return null;
      MediaItemAspect providerAspect = item[ProviderResourceAspect.ASPECT_ID];
      string systemId = (string) providerAspect[ProviderResourceAspect.ATTR_SYSTEM_ID];
      string resourceAccessorPath = (string) providerAspect[ProviderResourceAspect.ATTR_RESOURCE_ACCESSOR_PATH];
      return new ResourceLocator(systemId, ResourcePath.Deserialize(resourceAccessorPath));
    }

    public ICollection<Guid> GetMetadataExtractorsForCategory(string mediaCategory)
    {
      return new List<Guid>(LocalMetadataExtractors.Values.Select(
          metadataExtractor => new KeyValuePair<MetadataExtractorMetadata, IMetadataExtractor>(metadataExtractor.Metadata, metadataExtractor)).Where(md2me =>
          mediaCategory == null || md2me.Key.ShareCategories.Contains(mediaCategory)).Select(md2me => md2me.Key.MetadataExtractorId));
    }

    public ICollection<Guid> GetMetadataExtractorsForMIATypes(IEnumerable<Guid> miaTypeIDs)
    {
      return LocalMetadataExtractors.Where(
          extractor => extractor.Value.Metadata.ExtractedAspectTypes.Keys.Intersect(miaTypeIDs).Count() > 0).Select(
          kvp => kvp.Key).ToList();
    }

    public IDictionary<Guid, MediaItemAspect> ExtractMetadata(IResourceAccessor mediaItemAccessor,
        IEnumerable<Guid> metadataExtractorIds, bool forceQuickMode)
    {
      ICollection<IMetadataExtractor> extractors = new List<IMetadataExtractor>();
      foreach (Guid extractorId in metadataExtractorIds)
      {
        IMetadataExtractor extractor;
        if (LocalMetadataExtractors.TryGetValue(extractorId, out extractor))
          extractors.Add(extractor);
      }
      return ExtractMetadata(mediaItemAccessor, extractors, forceQuickMode);
    }

    public IDictionary<Guid, MediaItemAspect> ExtractMetadata(IResourceAccessor mediaItemAccessor,
        IEnumerable<IMetadataExtractor> metadataExtractors, bool forceQuickMode)
    {
      IDictionary<Guid, MediaItemAspect> result = new Dictionary<Guid, MediaItemAspect>();
      bool success = false;
      foreach (IMetadataExtractor extractor in metadataExtractors)
      {
        try
        {
          if (extractor.TryExtractMetadata(mediaItemAccessor, result, forceQuickMode))
            success = true;
        }
        catch (Exception e)
        {
          MetadataExtractorMetadata mem = extractor.Metadata;
          ServiceRegistration.Get<ILogger>().Error("MediaAccessor: Error extracting metadata from metadata extractor '{0}' (Id: '{1}')",
              e, mem.Name, mem.MetadataExtractorId);
          throw;
        }
      }
      return success ? result : null;
    }

    public MediaItem CreateLocalMediaItem(IResourceAccessor mediaItemAccessor, IEnumerable<Guid> metadataExtractorIds)
    {
      ISystemResolver systemResolver = ServiceRegistration.Get<ISystemResolver>();
      const bool forceQuickMode = true;
      IDictionary<Guid, MediaItemAspect> aspects = ExtractMetadata(mediaItemAccessor, metadataExtractorIds, forceQuickMode);
      if (aspects == null)
        return null;
      MediaItemAspect providerResourceAspect;
      if (aspects.ContainsKey(ProviderResourceAspect.ASPECT_ID))
        providerResourceAspect = aspects[ProviderResourceAspect.ASPECT_ID];
      else
        providerResourceAspect = aspects[ProviderResourceAspect.ASPECT_ID] = new MediaItemAspect(
            ProviderResourceAspect.Metadata);
      providerResourceAspect.SetAttribute(ProviderResourceAspect.ATTR_SYSTEM_ID, systemResolver.LocalSystemId);
      providerResourceAspect.SetAttribute(ProviderResourceAspect.ATTR_RESOURCE_ACCESSOR_PATH, mediaItemAccessor.CanonicalLocalResourcePath.Serialize());
      return new MediaItem(Guid.Empty, aspects);
    }

    #endregion

    #region IStatus Implementation

    public IList<string> GetStatus()
    {
      List<string> status = new List<string> {"=== MediaManager - ResourceProviders"};
      status.AddRange(_providers.Values.Select(provider => string.Format("     Provider '{0}'", provider.Metadata.Name)));
      status.Add("=== MediaManager - MetadataExtractors");
      status.AddRange(_metadataExtractors.Values.Select(metadataExtractor => string.Format("     MetadataExtractor '{0}'", metadataExtractor.Metadata.Name)));
      return status;
    }

    #endregion
  }
}
