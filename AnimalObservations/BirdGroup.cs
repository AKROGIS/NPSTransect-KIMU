using System.ComponentModel;
using System.Text;

//BirdGroup is bound to the XAML interface, BirdGroupFeature is bound to the GIS database 

namespace AnimalObservations
{
    public class BirdGroup : IDataErrorInfo
    {

        //public properties for WPF/XAML interface binding
        public int GroupSize { get; set; }
        public BirdGroupBehavior Behavior { get; set; }
        public BirdGroupSpecies Species { get; set; }
        public string Comment { get; set; }

        private BirdGroupFeature BirdGroupFeature { get; set; }


        #region Constructors

        public BirdGroup()
        {
        }

        internal BirdGroup(BirdGroupFeature birdGroupFeature)
        {
            GroupSize = birdGroupFeature.Size;
            switch (birdGroupFeature.Species)
            {
                case 'M':
                case 'm':
                    Species = BirdGroupSpecies.Marbled;
                    break;
                case 'K':
                case 'k':
                    Species = BirdGroupSpecies.Kittlitz;
                    break;
                case 'U':
                case 'u':
                    Species = BirdGroupSpecies.Unidentified;
                    break;
                default:
                    Species = BirdGroupSpecies.Pending;
                    break;
            }
            switch (birdGroupFeature.Behavior)
            {
                case 'W':
                case 'w':
                    Behavior = BirdGroupBehavior.Water;
                    break;
                case 'F':
                case 'f':
                    Behavior = BirdGroupBehavior.Flying;
                    break;
                default:
                    Species = BirdGroupSpecies.Pending;
                    break;
            }
            Comment = birdGroupFeature.Comments;
            BirdGroupFeature = birdGroupFeature;
        }

        #endregion


        #region Save/Delete

        public void Delete()
        {
            if (BirdGroupFeature != null)
                BirdGroupFeature.Delete();
        }

        public bool Save(Observation observation)
        {
            if (BirdGroupFeature == null)
                BirdGroupFeature = BirdGroupFeature.FromObservation(observation);
            BirdGroupFeature.Size = GroupSize;
            BirdGroupFeature.Behavior = Behavior.ToString()[0];
            BirdGroupFeature.Species = Species.ToString()[0];
            BirdGroupFeature.Comments = Comment;
            return BirdGroupFeature.Save();
        }

        #endregion


        #region IDataErrorInfo Members

        public string Error
        {
            get
            {
                var error = new StringBuilder();

                // iterate over all of the properties of this object - aggregating any validation errors
                PropertyDescriptorCollection props = TypeDescriptor.GetProperties(this);
                foreach (PropertyDescriptor prop in props)
                {
                    string propertyError = this[prop.Name];
                    if (propertyError != string.Empty)
                    {
                        error.Append((error.Length != 0 ? ", " : "") + propertyError);
                    }
                }
                //Check the Error property of the underlying database object
                if (BirdGroupFeature != null && !string.IsNullOrEmpty(BirdGroupFeature.Error))
                    error.Append((error.Length != 0 ? ", " : "") + BirdGroupFeature.Error);
                return error.ToString();
            }
        }

        public string this[string columnName]
        {
            get
            {
                // apply property level validation rules
                if (columnName == "Behavior")
                {
                    if (Behavior == BirdGroupBehavior.Pending)
                        return "Behavior cannot be Pending";
                }

                if (columnName == "GroupSize")
                {
                    if (GroupSize <= 0 || GroupSize >= 100)
                        return "GroupSize must be positive and less than 100";
                }

                return "";
            }
        }

        #endregion

        public override string ToString()
        {
            return string.Format("{0} {1}({2})", GroupSize, Species, Behavior);
        }

    }
}
