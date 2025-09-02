namespace API.Dtos;

public class UpdateDeckCardDto
{
    public int MaindeckQuantity { get; set; }
    public int SideboardQuantity { get; set; }
    public int? OwnedCardId { get; set; }
}