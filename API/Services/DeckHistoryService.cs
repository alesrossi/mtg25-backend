using API.Dtos.Cards;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.AspNetCore.Http;

namespace API.Services;

public interface IDeckHistoryService
{
    Task<DeckCommit> InitializeDeckHistoryAsync(int deckId, string userId, string branchName = "main", string message = "Initial commit", CancellationToken cancellationToken = default);
    Task<DeckCommit> CommitAsync(int deckId, string userId, string branchName, string message, CancellationToken cancellationToken = default);
    Task<DeckBranch> CreateBranchAsync(int deckId, string userId, string branchName, int fromCommitId, CancellationToken cancellationToken = default);
    Task CheckoutAsync(int deckId, string userId, int commitId, CancellationToken cancellationToken = default);
    Task<DeckDiffResult> DiffAsync(int deckId, string userId, int fromCommitId, int toCommitId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeckBranch>> GetBranchesAsync(int deckId, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeckCommit>> GetCommitsAsync(int deckId, string userId, CancellationToken cancellationToken = default);
}

public sealed class DeckHistoryService : IDeckHistoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly CardDataService _cardDataService;

    public DeckHistoryService(IUnitOfWork unitOfWork, CardDataService cardDataService)
    {
        _unitOfWork = unitOfWork;
        _cardDataService = cardDataService;
    }

    public async Task<DeckCommit> InitializeDeckHistoryAsync(int deckId, string userId, string branchName = "main", string message = "Initial commit", CancellationToken cancellationToken = default)
    {
        var deck = await RequireDeckAsync(deckId, userId);

        var existingBranch = await _unitOfWork.Repository<DeckBranch>()
            .GetEntityWithSpec(new DeckBranchByDeckIdAndNameSpecification(deckId, branchName), tracking: false);
        if (existingBranch != null)
        {
            throw DeckHistoryServiceException.Conflict("Errors.Decks.BranchAlreadyExists");
        }

        var deckCards = await _unitOfWork.Repository<DeckCard>()
            .ListAsync(new DeckCardsWithDeckIdSpecification(deckId), tracking: false);

        var tree = BuildTreeFromDeckCards(deckCards ?? []);
        var commit = new DeckCommit
        {
            DeckId = deck.Id,
            Tree = tree,
            AuthorId = userId,
            Message = message,
            CommittedAt = DateTime.UtcNow
        };

        var branch = new DeckBranch
        {
            DeckId = deck.Id,
            Name = branchName,
            HeadCommit = commit,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.Repository<DeckTree>().Add(tree);
        _unitOfWork.Repository<DeckCommit>().Add(commit);
        _unitOfWork.Repository<DeckBranch>().Add(branch);
        await _unitOfWork.Complete();

        deck.CurrentBranchId = branch.Id;
        deck.CurrentCommitId = commit.Id;
        _unitOfWork.Repository<Deck>().Update(deck);
        await _unitOfWork.Complete();

        return commit;
    }

    public async Task<DeckCommit> CommitAsync(int deckId, string userId, string branchName, string message, CancellationToken cancellationToken = default)
    {
        var deck = await RequireDeckAsync(deckId, userId);

        var branch = await _unitOfWork.Repository<DeckBranch>()
            .GetEntityWithSpec(new DeckBranchByDeckIdAndNameSpecification(deckId, branchName));
        if (branch == null)
        {
            throw DeckHistoryServiceException.NotFound("Errors.Decks.BranchNotFound");
        }

        var deckCards = await _unitOfWork.Repository<DeckCard>()
            .ListAsync(new DeckCardsWithDeckIdSpecification(deckId), tracking: false);

        var tree = BuildTreeFromDeckCards(deckCards ?? []);
        var commit = new DeckCommit
        {
            DeckId = deck.Id,
            Tree = tree,
            AuthorId = userId,
            Message = message,
            CommittedAt = DateTime.UtcNow
        };

        _unitOfWork.Repository<DeckTree>().Add(tree);
        _unitOfWork.Repository<DeckCommit>().Add(commit);

        if (branch.HeadCommitId.HasValue)
        {
            var parentLink = new DeckCommitParent
            {
                Commit = commit,
                ParentCommitId = branch.HeadCommitId.Value
            };
            _unitOfWork.Repository<DeckCommitParent>().Add(parentLink);
        }

        await _unitOfWork.Complete();

        branch.HeadCommit = commit;
        branch.HeadCommitId = commit.Id;
        _unitOfWork.Repository<DeckBranch>().Update(branch);
        await _unitOfWork.Complete();

        deck.CurrentBranchId = branch.Id;
        deck.CurrentCommitId = commit.Id;
        _unitOfWork.Repository<Deck>().Update(deck);
        await _unitOfWork.Complete();

        return commit;
    }

    public async Task<DeckBranch> CreateBranchAsync(int deckId, string userId, string branchName, int fromCommitId, CancellationToken cancellationToken = default)
    {
        var deck = await RequireDeckAsync(deckId, userId);

        var existingBranch = await _unitOfWork.Repository<DeckBranch>()
            .GetEntityWithSpec(new DeckBranchByDeckIdAndNameSpecification(deckId, branchName), tracking: false);
        if (existingBranch != null)
        {
            throw DeckHistoryServiceException.Conflict("Errors.Decks.BranchAlreadyExists");
        }

        var commit = await _unitOfWork.Repository<DeckCommit>().GetByIdAsync(fromCommitId, tracking: false);
        if (commit == null || commit.DeckId != deck.Id)
        {
            throw DeckHistoryServiceException.NotFound("Errors.Decks.CommitNotFound");
        }

        var branch = new DeckBranch
        {
            DeckId = deck.Id,
            Name = branchName,
            HeadCommitId = commit.Id,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.Repository<DeckBranch>().Add(branch);
        await _unitOfWork.Complete();

        return branch;
    }

    public async Task CheckoutAsync(int deckId, string userId, int commitId, CancellationToken cancellationToken = default)
    {
        var deck = await RequireDeckAsync(deckId, userId);

        var commit = await _unitOfWork.Repository<DeckCommit>().GetByIdAsync(commitId, tracking: false);
        if (commit == null || commit.DeckId != deck.Id)
        {
            throw DeckHistoryServiceException.NotFound("Errors.Decks.CommitNotFound");
        }

        var entries = await _unitOfWork.Repository<DeckTreeEntry>()
            .ListAsync(new DeckTreeEntriesByTreeIdSpecification(commit.TreeId), tracking: false);
        var entryLookup = (entries ?? [])
            .ToDictionary(entry => entry.ScryfallId, StringComparer.OrdinalIgnoreCase);

        var deckCards = await _unitOfWork.Repository<DeckCard>()
            .ListAsync(new DeckCardsWithDeckIdSpecification(deckId), tracking: true);
        var groupedDeckCards = (deckCards ?? [])
            .GroupBy(card => card.ScryfallId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groupedDeckCards)
        {
            var primary = group.First();
            if (entryLookup.TryGetValue(group.Key, out var entry))
            {
                primary.MaindeckQuantity = entry.MaindeckQuantity;
                primary.SideboardQuantity = entry.SideboardQuantity;
                _unitOfWork.Repository<DeckCard>().Update(primary);
                applied.Add(group.Key);

                foreach (var duplicate in group.Skip(1))
                {
                    _unitOfWork.Repository<DeckCard>().Delete(duplicate);
                }
            }
            else
            {
                foreach (var stale in group)
                {
                    _unitOfWork.Repository<DeckCard>().Delete(stale);
                }
            }
        }

        foreach (var entry in entryLookup.Values.Where(entry => !applied.Contains(entry.ScryfallId)))
        {
            var deckCard = CreateDeckCardFromEntry(deck.Id, entry);
            _unitOfWork.Repository<DeckCard>().Add(deckCard);
        }

        UpdateDeckAggregates(deck, entryLookup.Values);
        deck.DeckList = BuildDeckListFromEntries(entryLookup.Values);
        var branches = await _unitOfWork.Repository<DeckBranch>()
            .ListAsync(new DeckBranchesByDeckIdSpecification(deck.Id), tracking: false);
        var matchingBranch = branches?.FirstOrDefault(branch => branch.HeadCommitId == commitId);
        deck.CurrentBranchId = matchingBranch?.Id;
        deck.CurrentCommitId = commitId;
        _unitOfWork.Repository<Deck>().Update(deck);

        await _unitOfWork.Complete();
    }

    public async Task<DeckDiffResult> DiffAsync(int deckId, string userId, int fromCommitId, int toCommitId, CancellationToken cancellationToken = default)
    {
        var deck = await RequireDeckAsync(deckId, userId);

        var fromCommit = await _unitOfWork.Repository<DeckCommit>().GetByIdAsync(fromCommitId, tracking: false);
        var toCommit = await _unitOfWork.Repository<DeckCommit>().GetByIdAsync(toCommitId, tracking: false);

        if (fromCommit == null || toCommit == null)
        {
            throw DeckHistoryServiceException.NotFound("Errors.Decks.CommitNotFound");
        }

        if (fromCommit.DeckId != deck.Id && toCommit.DeckId != deck.Id)
        {
            throw DeckHistoryServiceException.NotFound("Errors.Decks.CommitNotFound");
        }

        await EnsureDeckOwnershipAsync(fromCommit.DeckId, userId);
        await EnsureDeckOwnershipAsync(toCommit.DeckId, userId);

        var fromEntries = await _unitOfWork.Repository<DeckTreeEntry>()
            .ListAsync(new DeckTreeEntriesByTreeIdSpecification(fromCommit.TreeId), tracking: false);
        var toEntries = await _unitOfWork.Repository<DeckTreeEntry>()
            .ListAsync(new DeckTreeEntriesByTreeIdSpecification(toCommit.TreeId), tracking: false);

        var fromLookup = (fromEntries ?? [])
            .ToDictionary(entry => entry.ScryfallId, StringComparer.OrdinalIgnoreCase);
        var toLookup = (toEntries ?? [])
            .ToDictionary(entry => entry.ScryfallId, StringComparer.OrdinalIgnoreCase);

        var keys = new HashSet<string>(fromLookup.Keys, StringComparer.OrdinalIgnoreCase);
        keys.UnionWith(toLookup.Keys);

        var added = new List<DeckDiffChange>();
        var removed = new List<DeckDiffChange>();
        var modified = new List<DeckDiffChange>();

        foreach (var key in keys)
        {
            fromLookup.TryGetValue(key, out var fromEntry);
            toLookup.TryGetValue(key, out var toEntry);

            var fromMain = fromEntry?.MaindeckQuantity ?? 0;
            var fromSide = fromEntry?.SideboardQuantity ?? 0;
            var toMain = toEntry?.MaindeckQuantity ?? 0;
            var toSide = toEntry?.SideboardQuantity ?? 0;

            if (fromEntry == null && toEntry != null)
            {
                added.Add(new DeckDiffChange(key, 0, 0, toMain, toSide));
                continue;
            }

            if (fromEntry != null && toEntry == null)
            {
                removed.Add(new DeckDiffChange(key, fromMain, fromSide, 0, 0));
                continue;
            }

            if (fromMain != toMain || fromSide != toSide)
            {
                modified.Add(new DeckDiffChange(key, fromMain, fromSide, toMain, toSide));
            }
        }

        return new DeckDiffResult(added, removed, modified);
    }

    public async Task<IReadOnlyList<DeckBranch>> GetBranchesAsync(int deckId, string userId, CancellationToken cancellationToken = default)
    {
        await RequireDeckAsync(deckId, userId);
        var branches = await _unitOfWork.Repository<DeckBranch>()
            .ListAsync(new DeckBranchesByDeckIdSpecification(deckId), tracking: false);
        return branches ?? [];
    }

    public async Task<IReadOnlyList<DeckCommit>> GetCommitsAsync(int deckId, string userId, CancellationToken cancellationToken = default)
    {
        await RequireDeckAsync(deckId, userId);
        var commits = await _unitOfWork.Repository<DeckCommit>()
            .ListAsync(new DeckCommitsByDeckIdSpecification(deckId), tracking: false);
        return commits ?? [];
    }

    private async Task<Deck> RequireDeckAsync(int deckId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw DeckHistoryServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId)
        {
            throw DeckHistoryServiceException.NotFound("Errors.Decks.NotFoundOrUnauthorized");
        }

        return deck;
    }

    private async Task EnsureDeckOwnershipAsync(int deckId, string userId)
    {
        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        if (deck == null || deck.OwnerId != userId)
        {
            throw DeckHistoryServiceException.NotFound("Errors.Decks.NotFoundOrUnauthorized");
        }
    }

    private static DeckTree BuildTreeFromDeckCards(IEnumerable<DeckCard> deckCards)
    {
        var tree = new DeckTree();
        foreach (var entry in BuildTreeEntries(deckCards))
        {
            entry.Tree = tree;
            tree.Entries.Add(entry);
        }

        return tree;
    }

    private static IEnumerable<DeckTreeEntry> BuildTreeEntries(IEnumerable<DeckCard> deckCards)
    {
        return deckCards
            .GroupBy(card => card.ScryfallId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DeckTreeEntry
            {
                ScryfallId = group.Key,
                MaindeckQuantity = group.Sum(card => card.MaindeckQuantity),
                SideboardQuantity = group.Sum(card => card.SideboardQuantity)
            })
            .Where(entry => entry.TotalQuantity > 0);
    }

    private DeckCard CreateDeckCardFromEntry(int deckId, DeckTreeEntry entry)
    {
        if (!_cardDataService.CardDataById.TryGetValue(entry.ScryfallId, out var cardData))
        {
            throw DeckHistoryServiceException.BadRequest("Errors.Decks.CardDataMissing", entry.ScryfallId);
        }

        var imageUris = CardDataService.ResolveImageUris(cardData);
        var imageUrl = imageUris?.Normal ?? imageUris?.Large ?? imageUris?.Png;

        if (string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(imageUris?.ArtCrop))
        {
            throw DeckHistoryServiceException.BadRequest("Errors.Decks.CardImageMissing", entry.ScryfallId);
        }

        return new DeckCard
        {
            DeckId = deckId,
            ScryfallId = cardData.Id,
            Name = cardData.Name,
            SetCode = cardData.Set,
            SetName = cardData.SetName,
            TypeLine = string.IsNullOrWhiteSpace(cardData.TypeLine) ? "Card" : cardData.TypeLine,
            ColorIdentity = cardData.ColorIdentity?.ToList() ?? [],
            ImageUrl = imageUrl,
            BackImageUrl = _cardDataService.ResolveBackImageUrl(cardData),
            ArtCrop = imageUris.ArtCrop!,
            Rarity = cardData.Rarity,
            CollectorNumber = cardData.CollectorNumber,
            MaindeckQuantity = entry.MaindeckQuantity,
            SideboardQuantity = entry.SideboardQuantity
        };
    }

    private static void UpdateDeckAggregates(Deck deck, IEnumerable<DeckTreeEntry> entries)
    {
        var main = entries.Sum(entry => entry.MaindeckQuantity);
        var side = entries.Sum(entry => entry.SideboardQuantity);
        deck.NumberOfMainBoardCards = main;
        deck.NumberOfSideBoardCards = side;
        deck.NumberOfCards = main + side;
    }

    private string BuildDeckListFromEntries(IEnumerable<DeckTreeEntry> entries)
    {
        var resolved = entries
            .Where(entry => entry.TotalQuantity > 0)
            .Select(entry =>
            {
                if (!_cardDataService.CardDataById.TryGetValue(entry.ScryfallId, out var cardData))
                {
                    throw DeckHistoryServiceException.BadRequest("Errors.Decks.CardDataMissing", entry.ScryfallId);
                }

                return new
                {
                    entry.MaindeckQuantity,
                    entry.SideboardQuantity,
                    Name = cardData.Name
                };
            })
            .ToList();

        var maindeckLines = resolved
            .Where(card => card.MaindeckQuantity > 0)
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(card => $"{card.MaindeckQuantity} {card.Name}")
            .ToList();

        var sideboardLines = resolved
            .Where(card => card.SideboardQuantity > 0)
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(card => $"{card.SideboardQuantity} {card.Name}")
            .ToList();

        if (maindeckLines.Count == 0 && sideboardLines.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>(maindeckLines);
        if (sideboardLines.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(sideboardLines);
        }

        return string.Join('\n', lines);
    }
}

public sealed record DeckDiffResult(
    IReadOnlyList<DeckDiffChange> Added,
    IReadOnlyList<DeckDiffChange> Removed,
    IReadOnlyList<DeckDiffChange> Modified);

public sealed record DeckDiffChange(
    string ScryfallId,
    int OldMaindeckQuantity,
    int OldSideboardQuantity,
    int NewMaindeckQuantity,
    int NewSideboardQuantity);

public sealed class DeckHistoryServiceException : Exception
{
    public int StatusCode { get; }
    public object? Body { get; }
    public bool IncludeBody { get; }

    private DeckHistoryServiceException(int statusCode, string? message, object? body, bool includeBody, Exception? innerException = null)
        : base(message ?? string.Empty, innerException)
    {
        StatusCode = statusCode;
        Body = body;
        IncludeBody = includeBody;
    }

    public static DeckHistoryServiceException Unauthorized(string message)
        => new(StatusCodes.Status401Unauthorized, message, null, false);

    public static DeckHistoryServiceException NotFound(string message)
        => new(StatusCodes.Status404NotFound, message, null, false);

    public static DeckHistoryServiceException Conflict(string message)
        => new(StatusCodes.Status409Conflict, message, null, false);

    public static DeckHistoryServiceException BadRequest(string message, object? body)
        => new(StatusCodes.Status400BadRequest, message, body, includeBody: true);
}
