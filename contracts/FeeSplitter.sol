// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/// @title  FeeSplitter — one-transaction ETH send with a platform fee
/// @notice Forwards an ETH payment to a recipient and the developer fee to a fixed address, in a
///         SINGLE transaction, so the fee costs no extra gas beyond the split. The wallet decides
///         the split (fee-on-top: it sends `recipientAmount + fee` as msg.value); the contract just
///         forwards both legs atomically and reverts if either fails.
///
///         Non-custodial by construction: it never keeps funds. Every wei received in a call is
///         forwarded within that same call, or the whole transaction reverts. There is no owner,
///         no withdraw, no upgrade — nothing that can trap or divert funds later.
contract FeeSplitter {
    /// @notice The developer fee recipient — hardcoded and immutable, so a deploy cannot point it
    ///         elsewhere and there is no setter to change it later. (EIP-55 checksummed.)
    address payable public constant feeRecipient =
        payable(0x01d1a1413F6b15f58906c804c261AFc12C3DCdBe);

    /// @notice Hard cap on the fee: it may never exceed 2% of the total sent, whatever the caller
    ///         passes. The recipient always receives at least 98% of msg.value.
    uint16 public constant maxFeeBps = 200;

    event Paid(address indexed recipient, uint256 recipientAmount, uint256 fee);

    error BadInput();
    error FeeTooHigh();
    error TransferFailed();

    /// @notice Send `recipientAmount` to `recipient`; the remainder of msg.value is the fee.
    /// @param  recipient        who receives the payment
    /// @param  recipientAmount  exactly how much the recipient gets; msg.value - this = the fee
    function pay(address payable recipient, uint256 recipientAmount) external payable {
        if (recipient == address(0) || recipientAmount == 0 || recipientAmount > msg.value) {
            revert BadInput();
        }

        uint256 fee = msg.value - recipientAmount;
        // Guard: the fee can never exceed maxFeeBps of the total sent.
        if (fee * 10000 > uint256(maxFeeBps) * msg.value) revert FeeTooHigh();

        (bool okRecipient, ) = recipient.call{value: recipientAmount}("");
        if (!okRecipient) revert TransferFailed();

        if (fee > 0) {
            (bool okFee, ) = feeRecipient.call{value: fee}("");
            if (!okFee) revert TransferFailed();
        }

        emit Paid(recipient, recipientAmount, fee);
    }
}
