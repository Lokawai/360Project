using System;
using UnityEngine.Localization.Settings;
using UnityEngine.Pool;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UnityEngine.Localization.Operations
{
    class LoadAssetOperation<TObject> : WaitForCurrentOperationAsyncOperationBase<TObject> where TObject : Object
    {
        readonly Action<AsyncOperationHandle<TObject>> m_AssetLoadedAction;
        AsyncOperationHandle<LocalizedAssetDatabase.TableEntryResult> m_TableEntryOperation;
        bool m_AutoRelease;

        public LoadAssetOperation()
        {
            m_AssetLoadedAction = AssetLoaded;
        }

        public void Init(AsyncOperationHandle<LocalizedAssetDatabase.TableEntryResult> loadTableEntryOperation, bool autoRelease)
        {
            m_TableEntryOperation = loadTableEntryOperation;
            AddressablesInterface.Acquire(m_TableEntryOperation);
            m_AutoRelease = autoRelease;
        }

        protected override void Execute()
        {
            if (m_TableEntryOperation.Status != AsyncOperationStatus.Succeeded)
            {
                Complete(null, false, "Load Table Entry Operation Failed");
                AddressablesInterface.Release(m_TableEntryOperation);
                return;
            }

            if (m_TableEntryOperation.Result.Table == null || m_TableEntryOperation.Result.Entry == null)
            {
                // Missing entries are treated as null objects
                CompleteAndRelease(default, true, null);
                return;
            }

            var loadAssetOperation = m_TableEntryOperation.Result.Table.GetAssetAsync<TObject>(m_TableEntryOperation.Result.Entry);
            if (loadAssetOperation.IsDone)
                AssetLoaded(loadAssetOperation);
            else
            {
                CurrentOperation = loadAssetOperation;
                loadAssetOperation.Completed += m_AssetLoadedAction;
            }
        }

        void AssetLoaded(AsyncOperationHandle<TObject> handle)
        {
            if (handle.Status != AsyncOperationStatus.Succeeded)
                CompleteAndRelease(null, false, "GetAssetAsync failed to load the asset.");
            else
                CompleteAndRelease(handle.Result, true, null);
        }

        public void CompleteAndRelease(TObject result, bool success, string errorMsg)
        {
            Complete(result, success, errorMsg);
            AddressablesInterface.Release(m_TableEntryOperation);

            if (m_AutoRelease && LocalizationSettings.Instance.IsPlaying)
            {
                // We need internal access for Handle here.
                LocalizationBehaviour.ReleaseNextFrame(Handle);
            }
        }

        protected override void Destroy()
        {
            base.Destroy();
            GenericPool<LoadAssetOperation<TObject>>.Release(this);
        }
    }
}
