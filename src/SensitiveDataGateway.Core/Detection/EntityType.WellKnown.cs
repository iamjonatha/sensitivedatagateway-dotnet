using System.Collections.Generic;

namespace SensitiveDataGateway.Detection;

/// <content>
/// The entity types the library recognizes out of the box.
/// </content>
/// <remarks>
/// These are conveniences, not a closed set. They exist so that the common categories are
/// spelled the same way in every policy, and so that a typo in a rule is a compilation error
/// rather than a rule that quietly never matches.
/// </remarks>
public readonly partial struct EntityType
{
    /// <summary>
    /// Gets the type for a person's name.
    /// </summary>
    public static EntityType Person { get; } = new("PERSON");

    /// <summary>
    /// Gets the type for an email address.
    /// </summary>
    public static EntityType Email { get; } = new("EMAIL");

    /// <summary>
    /// Gets the type for a telephone number.
    /// </summary>
    public static EntityType Phone { get; } = new("PHONE");

    /// <summary>
    /// Gets the type for a postal address.
    /// </summary>
    public static EntityType Address { get; } = new("ADDRESS");

    /// <summary>
    /// Gets the type for a tax identification number.
    /// </summary>
    public static EntityType TaxId { get; } = new("TAX_ID");

    /// <summary>
    /// Gets the type for a national identity number.
    /// </summary>
    public static EntityType NationalId { get; } = new("NATIONAL_ID");

    /// <summary>
    /// Gets the type for a passport number.
    /// </summary>
    public static EntityType Passport { get; } = new("PASSPORT");

    /// <summary>
    /// Gets the type for a driving licence number.
    /// </summary>
    public static EntityType DriverLicense { get; } = new("DRIVER_LICENSE");

    /// <summary>
    /// Gets the type for a date of birth.
    /// </summary>
    public static EntityType DateOfBirth { get; } = new("DATE_OF_BIRTH");

    /// <summary>
    /// Gets the type for an international bank account number.
    /// </summary>
    public static EntityType Iban { get; } = new("IBAN");

    /// <summary>
    /// Gets the type for a payment card number.
    /// </summary>
    public static EntityType CreditCard { get; } = new("CREDIT_CARD");

    /// <summary>
    /// Gets the type for a bank account number that is not an IBAN.
    /// </summary>
    public static EntityType BankAccount { get; } = new("BANK_ACCOUNT");

    /// <summary>
    /// Gets the type for a payment or invoice reference.
    /// </summary>
    public static EntityType FinancialReference { get; } = new("FINANCIAL_REFERENCE");

    /// <summary>
    /// Gets the type for a password.
    /// </summary>
    public static EntityType Password { get; } = new("PASSWORD");

    /// <summary>
    /// Gets the type for an API key.
    /// </summary>
    public static EntityType ApiKey { get; } = new("API_KEY");

    /// <summary>
    /// Gets the type for an access token.
    /// </summary>
    public static EntityType AccessToken { get; } = new("ACCESS_TOKEN");

    /// <summary>
    /// Gets the type for a JSON web token.
    /// </summary>
    public static EntityType Jwt { get; } = new("JWT");

    /// <summary>
    /// Gets the type for a private key.
    /// </summary>
    public static EntityType PrivateKey { get; } = new("PRIVATE_KEY");

    /// <summary>
    /// Gets the type for a connection string.
    /// </summary>
    public static EntityType ConnectionString { get; } = new("CONNECTION_STRING");

    /// <summary>
    /// Gets the type for the name of a customer.
    /// </summary>
    public static EntityType Customer { get; } = new("CUSTOMER");

    /// <summary>
    /// Gets the type for the name or code of a project.
    /// </summary>
    public static EntityType Project { get; } = new("PROJECT");

    /// <summary>
    /// Gets the type for a contract reference.
    /// </summary>
    public static EntityType Contract { get; } = new("CONTRACT");

    /// <summary>
    /// Gets the type for an identifier that is meaningful only inside the organization.
    /// </summary>
    public static EntityType InternalId { get; } = new("INTERNAL_ID");

    /// <summary>
    /// Gets the type for a code the organization treats as confidential.
    /// </summary>
    public static EntityType ConfidentialCode { get; } = new("CONFIDENTIAL_CODE");

    /// <summary>
    /// Gets the type for the name of an internal system.
    /// </summary>
    public static EntityType InternalSystem { get; } = new("INTERNAL_SYSTEM");

    /// <summary>
    /// Gets the type for health information.
    /// </summary>
    public static EntityType Health { get; } = new("HEALTH");

    /// <summary>
    /// Gets the type for information about legal proceedings.
    /// </summary>
    public static EntityType Legal { get; } = new("LEGAL");

    /// <summary>
    /// Gets the type for information about employment and personnel.
    /// </summary>
    public static EntityType Hr { get; } = new("HR");

    /// <summary>
    /// Gets the type for a financial situation described in prose rather than as a number.
    /// </summary>
    public static EntityType FinancialContext { get; } = new("FINANCIAL_CONTEXT");

    /// <summary>
    /// Gets the type for information about the security posture of a system.
    /// </summary>
    public static EntityType Security { get; } = new("SECURITY");

    /// <summary>
    /// Gets the type for a trade secret.
    /// </summary>
    public static EntityType TradeSecret { get; } = new("TRADE_SECRET");

    /// <summary>
    /// Gets every entity type the library recognizes out of the box.
    /// </summary>
    /// <remarks>
    /// The list is a checklist, not a taxonomy: a policy that governs all of it still has to
    /// say what happens to the categories this library has never heard of. Its order is not
    /// part of the contract.
    /// </remarks>
    public static IReadOnlyList<EntityType> WellKnown { get; } =
    [
        Person,
        Email,
        Phone,
        Address,
        TaxId,
        NationalId,
        Passport,
        DriverLicense,
        DateOfBirth,
        Iban,
        CreditCard,
        BankAccount,
        FinancialReference,
        Password,
        ApiKey,
        AccessToken,
        Jwt,
        PrivateKey,
        ConnectionString,
        Customer,
        Project,
        Contract,
        InternalId,
        ConfidentialCode,
        InternalSystem,
        Health,
        Legal,
        Hr,
        FinancialContext,
        Security,
        TradeSecret,
    ];
}
