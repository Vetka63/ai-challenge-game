using UnityEngine;
using Zenject;

namespace AsciiArena
{
    public sealed class ArenaInstaller : MonoInstaller
    {
        public ArenaSettings settings;
        public override void InstallBindings()
        {
            // A restored run owns its tuning; never overwrite the project ScriptableObject.
            Container.BindInstance(Instantiate(settings));
            Container.Bind<ArenaLayout>().AsSingle();
            Container.BindInterfacesAndSelfTo<ArenaSimulation>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<ArenaSession>().AsSingle().NonLazy();
            Container.BindInterfacesTo<ArenaInput>().FromComponentInHierarchy().AsSingle();
            Container.BindExecutionOrder<ArenaInput>(-100);
            Container.BindExecutionOrder<ArenaSimulation>(0);
            Container.BindExecutionOrder<ArenaSession>(10);
        }
    }
}
