using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace AsciiArena
{
    // Run-local build rules. UI and presentation consume this state; they never apply damage.
    public sealed partial class ArenaSimulation
    {
        private readonly WeaponKind[] weapons = { WeaponKind.None, WeaponKind.None };
        public int OfferCount { get; private set; }
        public WeaponKind WeaponAt(int slot) => slot>=0&&slot<2 ? weapons[slot] : WeaponKind.None;
        public bool HasWeapon(WeaponKind kind) => kind!=WeaponKind.None && (weapons[0]==kind||weapons[1]==kind);
        public int WeaponCount => weapons[1]!=WeaponKind.None?2:weapons[0]!=WeaponKind.None?1:0;
        public int OrbitHits { get; private set; }
        public int HurtCount { get; private set; }
        public int WarningCount { get; private set; }
        public float2 LastHurtSource { get; private set; }
        public float LastDamage { get; private set; }
        public float DashRemaining { get; private set; }
        public float DashCooldownRemaining { get; private set; }
        public float DashCharge => 1-math.saturate(DashCooldownRemaining/math.max(.1f,Settings.dashCooldown));
        private float2 dashDirection;
        private float echoAt=-1;
        private float2 echoPosition;
        private float orbitAngle;
        public int OrbitCount => 2+UpgradeRanks[(int)UpgradeKind.OrbitCount];
        public float OrbitDamage => Settings.orbitDamage*(1+.18f*UpgradeRanks[(int)UpgradeKind.OrbitPower]);
        public float PulseCooldown => math.max(.4f,Settings.pulseCooldown*math.pow(.92f,UpgradeRanks[1]));
        public float OrbitSpeed => Settings.orbitAngularSpeed*(1+.12f*UpgradeRanks[1]);
        public int BoltCount => 2+UpgradeRanks[(int)UpgradeKind.Fan];
        public float2 OrbitPosition(int blade,float time)
        {
            float angle=orbitAngle+(time-Elapsed)*OrbitSpeed+blade*math.PI*2/OrbitCount;
            return HeroPosition+ArenaLayout.ScreenToWorld(new float2(math.cos(angle),math.sin(angle))*Settings.orbitRadius);
        }

        private void ResetLoadout()
        {
            weapons[0]=weapons[1]=WeaponKind.None; OfferCount=3;
            DashRemaining=DashCooldownRemaining=orbitAngle=0;dashDirection=0;echoAt=-1;
            OrbitHits=HurtCount=WarningCount=0;LastHurtSource=0;LastDamage=0;
        }
        public bool TryDash()
        {
            if(Paused||Phase!=RunPhase.Running||DashCooldownRemaining>0||Dead)return false;
            var pose=World.EntityManager.GetComponentData<Hero>(HeroEntity);
            var fallback=pose.Facing==0?new float2(0,-1):pose.Facing==1?new float2(0,1):pose.Facing==2?new float2(-1,0):new float2(1,0);
            dashDirection=math.normalizesafe(Input,fallback);
            DashRemaining=math.max(.01f,Settings.dashDuration);
            DashCooldownRemaining=math.max(DashRemaining,Settings.dashCooldown);
            DemoMode=false;return true;
        }
        public static int RankCap(UpgradeKind kind) => kind switch
        { UpgradeKind.Pierce=>3,UpgradeKind.Fan=>2,UpgradeKind.Echo=>1,UpgradeKind.OrbitCount=>2,>=UpgradeKind.LearnBolt=>0,_=>5 };
        public bool Eligible(UpgradeKind kind)
        {
            if(kind>=UpgradeKind.LearnBolt&&kind<=UpgradeKind.LearnOrbit)
                return WeaponCount<2&&!HasWeapon((WeaponKind)((int)kind-(int)UpgradeKind.LearnBolt));
            if(kind==UpgradeKind.Recovery)return true;
            if(UpgradeRanks[(int)kind]>=RankCap(kind))return false;
            return kind switch
            {
                UpgradeKind.BoltPower or UpgradeKind.Pierce or UpgradeKind.Fan=>HasWeapon(WeaponKind.Runebolt),
                UpgradeKind.WideWard or UpgradeKind.Echo=>HasWeapon(WeaponKind.Ward),
                UpgradeKind.OrbitPower or UpgradeKind.OrbitCount=>HasWeapon(WeaponKind.Orbit),_=>true
            };
        }
        private void RollOffers()
        {
            var pool=new List<UpgradeKind>();OfferCount=0;
            // An empty second slot always gets an acquisition offer from level two onward.
            if(WeaponCount<2)
            {
                for(int k=11;k<=13;k++)if(Eligible((UpgradeKind)k))pool.Add((UpgradeKind)k);
                Offers[OfferCount++]=pool[random.NextInt(pool.Count)];pool.Clear();
            }
            for(int k=0;k<=13;k++)if(Eligible((UpgradeKind)k)&&!AlreadyOffered((UpgradeKind)k))pool.Add((UpgradeKind)k);
            while(OfferCount<3&&pool.Count>0){int i=random.NextInt(pool.Count);Offers[OfferCount++]=pool[i];pool.RemoveAt(i);}
            if(OfferCount<3)Offers[OfferCount++]=UpgradeKind.Recovery;
        }
        private bool AlreadyOffered(UpgradeKind kind){for(int i=0;i<OfferCount;i++)if(Offers[i]==kind)return true;return false;}
        public static string WeaponName(WeaponKind kind)=>kind switch
        { WeaponKind.Runebolt=>"DATA BOLT",WeaponKind.Ward=>"PULSE SHIELD",WeaponKind.Orbit=>"BIT ORBIT",_=>"EMPTY SLOT" };
        public static string WeaponDescription(WeaponKind kind)=>kind switch
        {
            WeaponKind.Runebolt=>"Auto-aimed bolts. Build into piercing or a wider volley.",
            WeaponKind.Ward=>"Close-range wave. Push enemies back; unlock a second pulse.",
            WeaponKind.Orbit=>"Two circling shards. Position the ring to cut through enemies.",_=>"Add a second weapon when you level up."
        };
        public float WeaponCharge(WeaponKind kind)=>kind switch
        { WeaponKind.Runebolt=>BoltCharge,WeaponKind.Ward=>PulseCharge,WeaponKind.Orbit=>1,_=>0 };
        public string WeaponSummary(WeaponKind kind)=>kind switch
        {
            WeaponKind.Runebolt=>$"{BoltDamage:0} DMG | x{BoltCount} | PIERCE {UpgradeRanks[6]}",
            WeaponKind.Ward=>$"{PulseDamage:0} DMG | {PulseRadius:0.0} R | ECHO {UpgradeRanks[8]}",
            WeaponKind.Orbit=>$"{OrbitDamage:0} DMG | {OrbitCount} SHARDS",_=>"CHOOSE ON LEVEL UP"
        };
        private void CastWard(float2 center,NativeArray<Entity> enemies,float multiplier=1)
        {
            var manager=World.EntityManager;
            PulseCasts++;Bursts.Add(new VisualBurst(center,Elapsed,1));
            foreach(var entity in enemies)
            {
                if(!manager.Exists(entity))continue;
                var p=manager.GetComponentData<Position2D>(entity).Value;
                if(math.distancesq(center,p)>PulseRadius*PulseRadius)continue;
                Damage(entity,PulseDamage*multiplier);
                if(manager.Exists(entity))
                {
                    var target=manager.GetComponentData<Enemy>(entity);
                    target.Knockback+=math.normalizesafe(p-center)*(target.Boss?.7f:target.Kind==2?4:9);
                    manager.SetComponentData(entity,target);
                }
                PulseHits++;
            }
        }
        private void UpdateOrbit(float delta,NativeArray<Entity> enemies,float2 oldHero)
        {
            if(!HasWeapon(WeaponKind.Orbit))return;
            var manager=World.EntityManager;
            foreach(var entity in enemies)
            {
                if(!manager.Exists(entity))continue;
                var enemy=manager.GetComponentData<Enemy>(entity);
                if(enemy.OrbitHitUntil>Elapsed)continue;
                var position=ArenaLayout.Project(manager.GetComponentData<Position2D>(entity).Value);
                for(int blade=0;blade<OrbitCount;blade++)
                {
                    if(SegmentDistanceSquared(position,ArenaLayout.Project(OrbitPosition(blade,Elapsed-delta)+oldHero-HeroPosition),ArenaLayout.Project(OrbitPosition(blade,Elapsed)))>(enemy.Boss?.8f:.3f))continue;
                    enemy.OrbitHitUntil=Elapsed+math.max(.1f,Settings.orbitHitInterval);manager.SetComponentData(entity,enemy);
                    Damage(entity,OrbitDamage);OrbitHits++;break;
                }
            }
        }
    }
}
