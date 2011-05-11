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
                       (GroupSize > 9 || GroupSize > 0 && !_previousCharacterWasDigit);
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

        public bool AcceptKey(char character)
        {
            if (!Char.IsLetterOrDigit(character))
            {
                _previousCharacterWasDigit = false;
                return false;
            }

            if (Char.IsDigit(character))
            {
                if (GroupSize > 0 && !_previousCharacterWasDigit)
                {
                    //Digit-nondigit-digit sequence is invalid for a single bird.
                    _previousCharacterWasDigit = true;
                    return false;
                }
                _previousCharacterWasDigit = true;
                if (GroupSize > 9)
                    return false;
                int digit = int.Parse(character.ToString());
                GroupSize = GroupSize * 10 + digit;
                return true;
            }
            _previousCharacterWasDigit = false;
            switch (character)
            {
                case 'W':
                case 'w':
                    if (Behavior != BirdGroupBehavior.Pending)
                        return false;
                    Behavior = BirdGroupBehavior.Water;
                    return true;
                case 'F':
                case 'f':
                    if (Behavior != BirdGroupBehavior.Pending)
                        return false;
                    Behavior = BirdGroupBehavior.Flying;
                    return true;
                case 'M':
                case 'm':
                    if (Species != BirdGroupSpecies.Pending)
                        return false;
                    Species = BirdGroupSpecies.Marbled;
                    return true;
                case 'K':
                case 'k':
                    if (Species != BirdGroupSpecies.Pending)
                        return false;
                    Species = BirdGroupSpecies.Kitlitz;
                    return true;
                case 'U':
                case 'u':
                    if (Species != BirdGroupSpecies.Pending)
                        return false;
                    Species = BirdGroupSpecies.Unidentified;
                    return true;
                default:
                    return false;
            }
        }

        private bool _previousCharacterWasDigit;

    }
}
