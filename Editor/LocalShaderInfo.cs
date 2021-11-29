using System.Collections.Generic;

namespace BundleKit.Assets
{

    [System.Serializable]
    public struct LocalShaderInfo
    {
        public string name;
        public bool hasErrors;
        public bool supported;

        public LocalShaderInfo(string name, bool hasErrors, bool supported)
        {
            this.name = name;
            this.hasErrors = hasErrors;
            this.supported = supported;
        }

        public override bool Equals(object obj)
        {
            return obj is LocalShaderInfo other &&
                   name == other.name &&
                   hasErrors == other.hasErrors &&
                   supported == other.supported;
        }

        public override int GetHashCode()
        {
            int hashCode = -259640393;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(name);
            hashCode = hashCode * -1521134295 + hasErrors.GetHashCode();
            hashCode = hashCode * -1521134295 + supported.GetHashCode();
            return hashCode;
        }

        public void Deconstruct(out string name, out bool hasErrors, out bool supported)
        {
            name = this.name;
            hasErrors = this.hasErrors;
            supported = this.supported;
        }

        public static implicit operator (string name, bool hasErrors, bool supported)(LocalShaderInfo value)
        {
            return (value.name, value.hasErrors, value.supported);
        }

        public static implicit operator LocalShaderInfo((string name, bool hasErrors, bool supported) value)
        {
            return new LocalShaderInfo(value.name, value.hasErrors, value.supported);
        }
    }
}
