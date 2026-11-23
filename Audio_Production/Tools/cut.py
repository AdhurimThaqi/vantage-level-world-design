import sys, os, shutil; sys.path.insert(0,'.')
from wav import *
SR=44100
ROOT="C:/Users/at488/OneDrive - Hochschule Luzern/Desktop/Level_World_Design_/"
A=ROOT+"Vantage_First-Person/Assets/Audio/"
R=ROOT+"Audio_Production/Renders/"
def keep(name, sub="SFX/"):
    src=A+sub+name+".wav"; dst=R+name+".wav"
    if not os.path.exists(dst): shutil.copy2(src,dst)
    return read(dst)
def fade(x, fin=0.003, fout=0.03):
    x=x.copy(); n=len(x)
    a=min(int(fin*SR),n//2); b=min(int(fout*SR),n//2)
    if a>0: x[:a]*=np.sin(np.linspace(0,np.pi/2,a))[:,None]**2
    if b>0: x[n-b:]*=np.cos(np.linspace(0,np.pi/2,b))[:,None]**2
    return x
def seg(x,t0,t1): return x[int(t0*SR):int(t1*SR)]
def mono(x): return x.mean(1)[:,None]
def loopxf(x, xf):
    # equal-power crossfade of the tail into the head; result length len(x)-xf
    n=int(xf*SR); body=x[n:].copy(); head=x[:n]; tail=body[-n:]
    t=np.linspace(0,np.pi/2,n)[:,None]
    body[-n:]=tail*np.cos(t)+head*np.sin(t)
    return body
def out(x, path, peak=None):
    if peak is not None: x=x*(10**(peak/20)/np.abs(x).max())
    write(A+path, x); print(f"{path}: {len(x)/SR:.3f}s peak {db(np.abs(x).max()):.1f}")

# One-shots: the sound, no trailing silence
out(fade(seg(keep("BOSS_Roar"),0.0,2.35),0.002,0.12),"SFX/BOSS_Roar.wav")
c=keep("COIN_Collect"); out(fade(c[int(104.50*SR):],0.002,0.04),"SFX/COIN_Collect.wav")
out(fade(seg(keep("KEYSLOT_Turn"),0.0,0.92),0.002,0.06),"SFX/KEYSLOT_Turn.wav")
out(fade(seg(keep("KEY_Pickup"),0.0,0.45),0.002,0.04),"SFX/KEY_Pickup.wav")
out(fade(seg(keep("PICKUP_Weapon"),0.03,0.84),0.003,0.06),"SFX/PICKUP_Weapon.wav")
out(fade(seg(keep("ROBOT_Deflect"),0.035,0.54),0.002,0.05),"SFX/ROBOT_Deflect.wav")
out(fade(seg(keep("ROBOT_Hit"),0.0,0.46),0.002,0.04),"SFX/ROBOT_Hit.wav")
out(fade(seg(keep("ROBOT_Servo"),0.13,0.90),0.01,0.08),"SFX/ROBOT_Servo.wav")
out(fade(seg(keep("SENTRY_Crack"),0.48,1.50),0.002,0.1),"SFX/SENTRY_Crack.wav")
out(fade(seg(keep("SENTRY_WarningCall"),0.0,1.62),0.002,0.06),"SFX/SENTRY_WarningCall.wav")
out(fade(seg(keep("VO_Contact"),0.015,0.44),0.002,0.04),"SFX/VO_Contact.wav")
# Heli cannon: one burst per pass (5 rounds 0.15 s apart = 0.75 s): rounds 1-8 then the last round with its natural tail
h=keep("HELI_Gun"); a=seg(h,0.0,0.70); b=seg(h,1.07,1.30); n=int(0.006*SR)
t=np.linspace(0,np.pi/2,n)[:,None]; j=np.concatenate([a[:-n], a[-n:]*np.cos(t)+b[:n]*np.sin(t), b[n:]])
out(fade(j,0.001,0.05),"SFX/HELI_Gun.wav")
# Metal steps: four single steps, levelled like the concrete ones (-12 dBFS)
m=keep("FS_Metal")
for i,t0 in enumerate([2.0,2.335,2.67,3.035]):
    out(fade(mono(seg(m,t0-0.008,t0+0.28)),0.002,0.08),f"SFX/FS_Metal_{i+1:02d}.wav",peak=-12)
# Boss reveal: the 8 s cutscene + its 2.5 s fade
out(fade(seg(keep("BOSS_Reveal"),0.3,10.8),0.01,2.6),"SFX/BOSS_Reveal.wav")
out(fade(seg(keep("STG_Cleared"),0.30,2.712),0.003,0.3),"SFX/STG_Cleared.wav")
# Stingers: one musical hit each, not the whole track
out(fade(seg(keep("STG_Lockdown"),15.98,21.0),0.004,1.8),"SFX/STG_Lockdown.wav")
out(fade(seg(keep("STG_BossFight"),20.88,26.5),0.004,2.0),"SFX/STG_BossFight.wav")
out(fade(seg(keep("STG_Extraction"),0.1,5.6),0.004,2.0),"SFX/STG_Extraction.wav")
# Swooshes: each cut so its peak sits 0.6 s in (the code starts it 0.6 s before a cut)
s=keep("CAM_SwitchSwoosheEffects")
for i,(pk,end) in enumerate([(0.461,1.35),(6.518,7.3),(9.950,11.6),(15.687,17.6)]):
    st=max(0,pk-0.6); x=seg(s,st,end); pad=int((0.6-(pk-st))*SR)
    x=np.concatenate([np.zeros((pad,2)),x]) if pad>0 else x
    out(fade(x,0.15,0.35),f"SFX/CAM_Swoosh_{i+1:02d}.wav")
# Music layers: same length (176 s = 88 bars of the 120 BPM combat layer), seam crossfaded 2 s
for name in ["MUS_Combat","MUS_Explore"]:
    x=keep(name); x=seg(x,0,178.0); out(loopxf(x,2.0),"Music/"+name+".wav")
