namespace Model
{
    public abstract class BaseEntity
    {
        // מזהה ראשי משותף לכל הישויות במערכת
        public int Id { get; set; }

        protected BaseEntity()
        {
        }

        protected BaseEntity(int id)
        {
            Id = id;
        }
    }
}