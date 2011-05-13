using System;

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

    public class BirdGroup2
    {
        //TODO - merge with BirdGroup (?? need a default constructor for Datagrid WPF/XAML interface)

        //public properties for WPF/XAML interface binding
        public int GroupSize { get; set; }
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
                DbLink = BirdGroup.CreateWith(observation);
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

    }
}
