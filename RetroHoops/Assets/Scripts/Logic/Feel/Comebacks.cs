namespace CallerRetroBall.Logic
{
    /// <summary>I15 COMEBACKS: when climbing back level gets called, and what a comeback win is.</summary>
    public static class Comebacks
    {
        /// <summary>Being down this much and tying it up gets "ALL TIED UP!" (double in Full Court's 2s and 3s).</summary>
        public static int TieCallFrom(bool fullCourt) => fullCourt ? MomentsBook.ComebackFrom * 2 : MomentsBook.ComebackFrom;

        /// <summary>A comeback win: down at least the threshold at some point, and won.</summary>
        public static bool IsComeback(bool won, int maxDeficit, bool fullCourt) => won && maxDeficit >= TieCallFrom(fullCourt);
    }
}
