import numpy as np, wave, struct, subprocess, sys
def read(path, sr=44100):
    # decode with ffmpeg to float32 stereo
    out = subprocess.run(['ffmpeg','-v','error','-i',path,'-f','f32le','-ac','2','-ar',str(sr),'-'],capture_output=True).stdout
    return np.frombuffer(out, dtype=np.float32).reshape(-1,2).copy()
def write(path, x, sr=44100, bits=24):
    x = np.asarray(x, dtype=np.float64)
    if x.ndim==1: x=x[:,None]
    ch=x.shape[1]
    x=np.clip(x,-1,1)
    if bits==24:
        v=(x*8388607).astype(np.int32).reshape(-1)
        b=np.zeros((v.size,3),np.uint8)
        b[:,0]=v&255; b[:,1]=(v>>8)&255; b[:,2]=(v>>16)&255
        data=b.tobytes()
    else:
        data=(x*32767).astype('<i2').tobytes()
    with wave.open(path,'wb') as w:
        w.setnchannels(ch); w.setsampwidth(bits//8); w.setframerate(sr); w.writeframes(data)
def db(v): return 20*np.log10(max(v,1e-12))
def rms(x): return np.sqrt(np.mean(np.square(x)))
def bands(x, sr=44100):
    m=x.mean(1) if x.ndim>1 else x
    n=len(m); f=np.fft.rfftfreq(n,1/sr); P=np.abs(np.fft.rfft(m))**2
    edges=[20,80,200,500,1000,2000,4000,8000,16000]
    tot=P.sum()
    return ' '.join(f"{a}-{b}:{10*np.log10(P[(f>=a)&(f<b)].sum()/tot+1e-12):.0f}" for a,b in zip(edges[:-1],edges[1:]))
