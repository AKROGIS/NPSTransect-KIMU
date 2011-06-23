using System;
using System.ComponentModel;
using System.Text;

namespace AnimalObservations
{
    public enum BirdGroupBehavior
    {
        Pending,
        Flying,
        Water
    }

    public enum BirdGroupSpecies
    {
        Pending,
        Kitlitz,
        Marbled,
        Unidentified
    }

    public class BirdGroup2 : IDataErrorInfo
    {
        //TODO - merge with BirdGroup (?? need a default constructor for Datagrid WPF/XAML interface)

        //public properties for WPF/XAML interface binding
        public int GroupSize { get; set; }
        //public int GroupSize
        //{
        //    get
        //    {
        //        return _groupSize;
        //    }
        //    set
        //    {
        //        if (value <= 0)
        //            throw new ArgumentOutOfRangeException("value", "GroupSize must be positive.");
        //        if (value >= 100)
        //            throw new ArgumentOutOfRangeException("value", "GroupSize must be less than 100.");
        //        _groupSize = value;
        //    }
        //}
        //private int _groupSize;

        public BirdGroupBehavior Behavior { get; set; }
        public BirdGroupSpecies Species { get; set; }
        public string Comment { get; set; }

        private BirdGroup DbLink { get; set; }

        #region Constructors

        public BirdGroup2()
        {
        }

        internal BirdGroup2(BirdGroup birdGroup)
        {
            GroupSize = birdGroup.Size;
            switch (birdGroup.Species)
            {
                case 'M':
                case 'm':
                    Species = BirdGroupSpecies.Marbled;
                    break;
                case 'K':
                case 'k':
                    Species = BirdGroupSpecies.Kitlitz;
                    break;
                case 'U':
                case 'u':
                    Species = BirdGroupSpecies.Unidentified;
                    break;
                default:
                    Species = BirdGroupSpecies.Pending;
                    break;
            }
            switch (birdGroup.Behavior)
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
            Comment = birdGroup.Comments;
            DbLink = birdGroup;
        }

        #endregion

        public void Delete()
        {
            if (DbLink != null)
                DbLink.Delete();
        }

        public bool Save(Observation observation)
        {
            if (DbLink == null)
                DbLink = BirdGroup.FromObservation(observation);
            DbLink.Size = GroupSize;
            DbLink.Behavior = Behavior.ToString()[0];
            DbLink.Species = Species.ToString()[0];
            DbLink.Comments = Comment;
            return DbLink.Save();
        }

        internal bool IsValid
        {
            get
            {
                return Behavior != BirdGroupBehavior.Pending &&
                       GroupSize > 0 &&
                       GroupSize < 100;
            }
        }

        internal bool IsComplete
        {
            get
            {
                return Behavior != BirdGroupBehavior.Pending &&
                       Species != BirdGroupSpecies.Pending &&
                       (9 < GroupSize || (0 < GroupSize && !_previousCharacterWasDigit));
            }
        }

        //TODO - remove this key code stuff

        public void Reset()
        {
            GroupSize = default(int);
            Behavior = default(BirdGroupBehavior);
            Species = default(BirdGroupSpecies);
            Comment = default(string);
            DbLink = default(BirdGroup);
            _previousCharacterWasDigit = default(bool);
        }

        internal static bool RecognizeKey(char character)
        {
            return "0123456789WwFfMmKkUu".Contains(character.ToString());
        }

        internal bool AcceptKey(char character)
        {
            if (!RecognizeKey(character))
                goto invalidNonDigit;

            //Digits
            if (Char.IsDigit(character))
            {
                int digit = int.Parse(character.ToString());

                //reject leading zeros
                if (digit == 0 && !_previousCharacterWasDigit)
                    goto invalidDigit;

                //reject digit-nondigit-digit sequence
                if (GroupSize > 0 && !_previousCharacterWasDigit)
                    goto invalidDigit;

                //reject third digit
                if (GroupSize > 9)
                    goto invalidDigit;

                //accept all other digits
                GroupSize = GroupSize * 10 + digit;
                goto validDigit;
            }

            //Recognized Non-digits
            switch (character)
            {
                case 'W':
                case 'w':
                    if (Behavior != BirdGroupBehavior.Pending)
                        goto invalidNonDigit;
                    Behavior = BirdGroupBehavior.Water;
                    goto validNonDigit;
                case 'F':
                case 'f':
                    if (Behavior != BirdGroupBehavior.Pending)
                        goto invalidNonDigit;
                    Behavior = BirdGroupBehavior.Flying;
                    goto validNonDigit;
                case 'M':
                case 'm':
                    if (Species != BirdGroupSpecies.Pending)
                        goto invalidNonDigit;
                    Species = BirdGroupSpecies.Marbled;
                    goto validNonDigit;
                case 'K':
                case 'k':
                    if (Species != BirdGroupSpecies.Pending)
                        goto invalidNonDigit;
                    Species = BirdGroupSpecies.Kitlitz;
                    goto validNonDigit;
                case 'U':
                case 'u':
                    if (Species != BirdGroupSpecies.Pending)
                        goto invalidNonDigit;
                    Species = BirdGroupSpecies.Unidentified;
                    goto validNonDigit;
                default:
                    goto invalidNonDigit;
            }
            invalidDigit:
                _previousCharacterWasDigit = true;
                return false;
            validDigit:
                _previousCharacterWasDigit = true;
                return true;
            invalidNonDigit:
                _previousCharacterWasDigit = false;
                return false;
            validNonDigit:
                _previousCharacterWasDigit = false;
                return true;
        }

        private bool _previousCharacterWasDigit;

        #region IDataErrorInfo Members

        public string Error
        {
            get
            {
                var error = new StringBuilder();

                // iterate over all of the properties
                // of this object - aggregating any validation errors
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
                if (DbLink != null && !string.IsNullOrEmpty(DbLink.Error))
                    error.Append((error.Length != 0 ? ", " : "") + DbLink.Error);
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
    }
}
