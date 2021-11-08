using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace VEngine
{
    public enum LoadableStatus
    {
        Wait,
        Loading,
        DependentLoading,
        SuccessToLoad,
        FailedToLoad,
        Unloaded,
        CheckVersion,
        Downloading
    }

    public class Loadable
    {
        public static readonly List<Loadable> Loading = new List<Loadable>();
        public static readonly List<Loadable> Unused = new List<Loadable>();

        protected readonly Reference reference = new Reference();
        public LoadableStatus status { get; protected set; } = LoadableStatus.Wait;
        public string pathOrURL { get; set; }
        protected bool mustCompleteOnNextFrame { get; set; }
        public string error { get; internal set; }

        public bool isDone => status == LoadableStatus.SuccessToLoad || status == LoadableStatus.Unloaded ||
                              status == LoadableStatus.FailedToLoad;

        public float progress { get; protected set; }
        public long elapsed { get; private set; }
        private Stopwatch watch;
        private int startFrame;

        public int frames { get; set; }

        protected void Finish(string errorCode = null)
        {
            error = errorCode;
            status = string.IsNullOrEmpty(errorCode) ? LoadableStatus.SuccessToLoad : LoadableStatus.FailedToLoad;
            progress = 1;
        }

        public static bool IsBlockingRemoveUnused
        {
            get { return Scene.current != null && !Scene.current.isDone; }
        }

        public static void UpdateLoadingAndUnused()
        {
            for (var index = 0; index < Loading.Count; index++)
            {
                var item = Loading[index];
                if (Updater.Instance.busy) return;

                item.Update();
                if (!item.isDone) continue;

                Loading.RemoveAt(index);
                index--;
                item.Complete();
            }

            if (IsBlockingRemoveUnused) return;

            for (var index = 0; index < Unused.Count; index++)
            {
                var item = Unused[index];
                if (Updater.Instance.busy) break;

                if (!item.isDone) continue;

                Unused.RemoveAt(index);
                index--;
                if (!item.reference.unused) continue;

                item.Unload();
            }
        }

        internal void Update()
        {
            OnUpdate();
        }

        internal void Complete()
        {
            if (status == LoadableStatus.FailedToLoad)
            {
                Logger.E("Unable to load {0} {1} with error: {2}", GetType().Name, pathOrURL, error);
                Release();
            }

            if (elapsed == 0)
            {
                watch.Stop();
                elapsed = watch.ElapsedMilliseconds;
                frames = UnityEngine.Time.frameCount - startFrame;
            }

            OnComplete();
        }

        protected virtual void OnUpdate()
        {
        }

        protected virtual void OnLoad()
        {
        }

        protected virtual void OnUnload()
        {
        }

        protected virtual void OnComplete()
        {
        }

        public virtual void LoadImmediate()
        {
            throw new InvalidOperationException();
        }

        protected internal void Load()
        {
            if (status != LoadableStatus.Wait && reference.unused)
                Unused.Remove(this);

            reference.Retain();
            Loading.Add(this);
            if (status != LoadableStatus.Wait) return;

            Logger.I("Load {0} {1}.", GetType().Name, Path.GetFileName(pathOrURL));
            status = LoadableStatus.Loading;
            progress = 0;
            watch = new Stopwatch();
            watch.Start();
            startFrame = UnityEngine.Time.frameCount;
            OnLoad();
        }

        protected internal void Unload()
        {
            if (status == LoadableStatus.Unloaded) return;

            Logger.I("Unload {0} {1}.", GetType().Name, Path.GetFileName(pathOrURL), error);
            OnUnload();
            status = LoadableStatus.Unloaded;
        }

        public void Release()
        {
            if (reference.count <= 0)
            {
                Logger.W("Release {0} {1}.", GetType().Name, Path.GetFileName(pathOrURL));
                return;
            }

            reference.Release();
            if (!reference.unused) return;

            Unused.Add(this);
            OnUnused();
        }

        protected virtual void OnUnused()
        {
        }
    }
}