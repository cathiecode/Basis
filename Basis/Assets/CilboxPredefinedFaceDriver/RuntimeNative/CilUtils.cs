using Cilbox;
using UnityEngine;

namespace com.superneko.basis.masque.native
{
    public class MasqueCilboxMethod
    {
        CilboxProxy _proxy;
        CilboxMethod _method;

        public MasqueCilboxMethod(CilboxProxy proxy, CilboxMethod method)
        {
            _proxy = proxy;
            _method = method;
        }

        public object Call(object[] parametersIn)
        {
            return _method.Interpret(_proxy, parametersIn);
        }

        public static bool TryFromProxy(CilboxProxy proxy, string methodName, out MasqueCilboxMethod mcm)
        {
            if (proxy == null) {
                mcm = null;
                return false;
            }

            var cls = proxy.cls;

            if (cls == null) {
                mcm = null;
                return false;
            }

            if (!cls.methodNameToIndex.TryGetValue(methodName, out var methodIndex)) {
                mcm = null;
                return false;
            }

            var method = cls.methods[methodIndex];

            if (method.isStatic) {
                mcm = null;
                return false;
            }

            mcm = new MasqueCilboxMethod(proxy, method);

            return true;
        }

        public static bool TryFromGameObjectAny(GameObject gameObject, string methodName, out MasqueCilboxMethod mcm)
        {
            if (!gameObject.TryGetComponent<CilboxProxy>(out var proxy))
            {
                mcm = null;
                return false;
            }

            return TryFromProxy(proxy, methodName, out mcm);
        }
    }
}
