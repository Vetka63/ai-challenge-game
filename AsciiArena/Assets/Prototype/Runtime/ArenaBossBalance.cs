using Unity.Collections;
using Unity.Mathematics;

namespace AsciiArena
{
    public sealed partial class ArenaSimulation
    {
        public const float BossTelegraphWindow = 1.35f;
        public const int BossCrowdLimit = 28;
        public float WardenChargeDamage => Settings.contactDamage * 3.5f;
        public float BossHazardDamage(BossHazard h) => Settings.enemyShotDamage * (h.Source == 1 ? 3.5f : 2.5f);
        public bool TrainingCrowd { get; private set; }
        public void EnableTrainingCrowd()
        {
            if(!Training||TrainingCrowd)return;
            TrainingCrowd=true;
            for(int i=0;i<24;i++)SpawnEnemy();
            var m=World.EntityManager;using var es=EnemyQuery.ToEntityArray(Allocator.Temp);int index=0;
            foreach(var entity in es)
            {
                var e=m.GetComponentData<Enemy>(entity);if(e.Boss)continue;
                e.Kind=index<6?(byte)(1+index%5):(byte)0;index++;
                e.Speed=BaseEnemySpeed(e.Kind);e.MaxHealth=BaseEnemyHealth(e.Kind)*1.5f;
                e.AttackTimer=1+(index%4)*.4f;
                m.SetComponentData(entity,e);m.SetComponentData(entity,new Health{Value=e.MaxHealth});
            }
        }
        // Reserve the next phrase before it begins. Regular attacks can finish, but
        // cannot indefinitely starve the boss or hide its next warning.
        public bool BossClaimsAttackWindow
        {
            get
            {
                if(!Settings.actOne||!BossActive)return false;
                var e=World.EntityManager.GetComponentData<Enemy>(BossEntity);
                return Hazards.Count>0||e.Windup>0||e.BossChargePending||e.ChargeLeft>0||e.AttackTimer<=.75f;
            }
        }
        private bool RegularDangerPending()
        {
            var m=World.EntityManager;
            using var es=EnemyQuery.ToComponentDataArray<Enemy>(Allocator.Temp);
            foreach(var e in es)
                if(!e.Boss&&(e.Windup>0||e.ChargeLeft>0||(e.Kind==Bombardier&&e.Recovery>0)))return true;
            using var bs=BoltQuery.ToEntityArray(Allocator.Temp);
            foreach(var entity in bs)
                if(m.GetComponentData<Bolt>(entity).Hostile&&math.lengthsq(ArenaLayout.Project(m.GetComponentData<Position2D>(entity).Value-HeroPosition))<144)return true;
            return false;
        }
        public float BossDamageMultiplier(Enemy e)
        {
            if(!Settings.actOne||!e.Boss)return 1;
            if(Hazards.Count==0&&e.Recovery>0&&e.Windup<=0&&e.ChargeLeft<=0)return 1.6f;
            return e.Windup>0||e.BossChargePending||e.ChargeLeft>0||Hazards.Count>0?.55f:1;
        }
        public static bool BossHazardVisible(BossHazard h) => h.Delay <= BossTelegraphWindow;
    }
}
