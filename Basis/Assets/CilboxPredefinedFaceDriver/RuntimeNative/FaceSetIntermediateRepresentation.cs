using System;

namespace com.superneko.basis.masque.native
{
    [Serializable]
    public struct SerializableExpression
    {
        public int ReferenceId;
        public float[] blendshapeWeights; // Weight of blendShape. Layout is same as controllingBlendShapes
    }

    [Serializable]
    public struct SerializableFaceSet
    {
        public int[] controllingBlendShapes; // List of blendShape index
        public SerializableExpression defaultExpression;
        public SerializableExpression[] expressions;
    }
}
