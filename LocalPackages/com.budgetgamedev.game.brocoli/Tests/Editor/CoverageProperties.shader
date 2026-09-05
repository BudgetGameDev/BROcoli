Shader "Hidden/Brocoli/CoverageProperties"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _BaseMap ("Base Map", 2D) = "white" {}
        _Main_Tex ("Main Underscore Texture", 2D) = "white" {}
        _Surface ("Surface", Float) = 0
        _SoftParticlesEnabled ("Soft Particles", Float) = 0
        _SoftParticleFadeParams ("Soft Fade", Vector) = (0,0,0,0)
        _SoftParticlesNearFadeDistance ("Soft Near", Float) = 0
        _SoftParticlesFarFadeDistance ("Soft Far", Float) = 0
        _Metallic ("Metallic", Float) = 0
        _Smoothness ("Smoothness", Float) = 0
        _Glossiness ("Glossiness", Float) = 0
    }
    SubShader
    {
        Pass { }
    }
}
