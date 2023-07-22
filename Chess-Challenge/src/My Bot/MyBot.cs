using System;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    // Piece values: pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 100, 300, 300, 500, 900, 10000 };
    int moveCount = 0;

    public int Evaluate(Board board)
    {
        if (board.IsInCheckmate()) { return board.IsWhiteToMove ? int.MinValue : int.MaxValue; }
        else if (board.IsDraw()) { return 0; }
        // Return a score for the given board state, positive means white has more.
        PieceList[] piecelists = board.GetAllPieceLists();
        int score = 0;
        // White pieces +, Black pieces -
        for (int i = 0; i < 6; i++)
        {
            score += piecelists[i].Count * pieceValues[i];
            score -= piecelists[i + 6].Count * pieceValues[i];
        }
        return score;
    }

    public int EvaluateMove(Board board, Move move)
    {
        board.MakeMove(move);
        int score = Evaluate(board);
        board.UndoMove(move);
        moveCount += 1;
        return score;
    }

    public (int, Move, Move) Search(Board board, int ply = 1)
    {
        if (ply == 0)
        {
            return (Evaluate(board), Move.NullMove, Move.NullMove);
        }
        // Evaluate score of board assuming optimal choices up to a ply.
        Move[] allMoves = board.GetLegalMoves();
        if (allMoves.Length == 0)
        {
            // No legal moves here, means we're in check or stalemate.
            if (board.IsInCheckmate()) { return (board.IsWhiteToMove ? int.MinValue : int.MaxValue, Move.NullMove, Move.NullMove); }
            else if (board.IsDraw()) { return (0, Move.NullMove, Move.NullMove); }
        }
        Move moveToPlay = Move.NullMove;
        Move expected_response = Move.NullMove;
        int bestScore = 0;

        // Choose random move as start move
        Random rng = new();
        moveToPlay = allMoves[rng.Next(allMoves.Length)];
        (bestScore, expected_response) = MoveAndSearch(board, moveToPlay, ply - 1);

        foreach (Move move in allMoves)
        {
            int moveScore;
            // Make move and assume evalute based on optimal response / continue ply
            (moveScore, Move response_move) = MoveAndSearch(board, move, ply - 1);

            // If score is > for white, or < for black
            if (moveToPlay == Move.NullMove ||
                (board.IsWhiteToMove && moveScore > bestScore) ||
                (!board.IsWhiteToMove && moveScore < bestScore))
            {
                moveToPlay = move;
                expected_response = response_move;
                bestScore = moveScore;
            }
        }
        return (bestScore, moveToPlay, expected_response);
    }

    public (int, Move) MoveAndSearch(Board board, Move move, int ply = 1)
    {
        board.MakeMove(move);
        int score;
        Move next_move = Move.NullMove;
        if (board.IsInCheckmate()) { score = board.IsWhiteToMove ? int.MinValue : int.MaxValue; }
        else if (board.IsDraw()) { score = 0; }
        else { (score, next_move, Move next_move_response) = Search(board, ply); }
        board.UndoMove(move);
        moveCount += 1;
        return (score, next_move);
    }

    public Move Think(Board board, Timer timer)
    {
        moveCount = 0;
        int ply = 3;
        (int bestScore, Move moveToPlay, Move expected_response) = Search(board, ply);
        return moveToPlay;
    }
}
