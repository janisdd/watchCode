namespace watchCode.model
{
    public class CommentPattern
    {
        public string StartCommentPart { get; set; }
        public string EndCommentPart { get; set; }


        public CommentPattern Clone()
        {
            return new CommentPattern()
            {
                StartCommentPart = this.StartCommentPart,
                EndCommentPart = this.EndCommentPart,
            };
        }
    }
}
