using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEngine
{
    public class Bundle : Loadable
    {
        public static Func<ManifestBundle, Bundle> customBundleCreator;

        public static readonly Dictionary<string, Bundle> Cache = new Dictionary<string, Bundle>();

        public ManifestBundle info;

        private static readonly Dictionary<string, AssetBundle> AssetBundles = new Dictionary<string, AssetBundle>();

        public AssetBundle assetBundle { get; protected set; }

        protected void OnLoaded(AssetBundle bundle)
        {
            assetBundle = bundle;
            Finish(assetBundle == null ? "assetBundle == null" : null);
            AssetBundles[info.name] = bundle;
        }

        public static Bundle Load(string assetPath)
        {
            return LoadInternal(Versions.GetBundle(assetPath), true);
        }

        public static Bundle LoadAsync(string assetPath)
        {
            return LoadInternal(Versions.GetBundle(assetPath), false);
        }

        internal static Bundle LoadInternal(ManifestBundle bundle, bool mustCompleteOnNextFrame)
        {
            if (bundle == null) throw new NullReferenceException();

            if (!Cache.TryGetValue(bundle.nameWithAppendHash, out var item))
            {
                if (AssetBundles.TryGetValue(bundle.name, out var assetBundle))
                {
                    assetBundle.Unload(false);
                    AssetBundles.Remove(bundle.name);
                }

                var url = Versions.GetBundlePathOrURL(bundle);
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    item = new WebBundle { pathOrURL = url, info = bundle };
                }
                else
                {
                    if (customBundleCreator != null) item = customBundleCreator(bundle);
                    if (item == null)
                    {
                        if (url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("ftp://"))
                            item = new DownloadBundle { pathOrURL = url, info = bundle };
                        else
                            item = new LocalBundle { pathOrURL = url, info = bundle };
                    }
                }

                Cache.Add(bundle.nameWithAppendHash, item);
            }

            item.mustCompleteOnNextFrame = mustCompleteOnNextFrame;
            item.Load();
            if (mustCompleteOnNextFrame) item.LoadImmediate();

            return item;
        }

        protected override void OnUnload()
        {
            if (assetBundle == null) return;
            assetBundle.Unload(true);
            assetBundle = null;
            AssetBundles.Remove(info.name);
            Cache.Remove(info.nameWithAppendHash);
        }
    }
}