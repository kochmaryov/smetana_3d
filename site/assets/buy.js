// smetana — заготовка формы покупки: вызывает POST /api/v1/checkout
// (контракт — docs/payments.md §4). Backend до подключения провайдеров отсутствует,
// поэтому запрос завершится ошибкой — интерфейс показывает понятный статус.
(function () {
  "use strict";

  var CHECKOUT_ENDPOINT = "/api/v1/checkout";

  function setStatus(el, message, kind) {
    el.textContent = message;
    el.className = "form-status" + (kind ? " " + kind : "");
  }

  document.addEventListener("DOMContentLoaded", function () {
    var form = document.getElementById("buy-form");
    if (!form) return;
    var status = document.getElementById("buy-status");

    // Тариф: карточки с data-tariff → скрытое поле.
    var tariffInput = form.querySelector("input[name='tariffId']");
    var cards = document.querySelectorAll("[data-tariff]");
    cards.forEach(function (card) {
      card.addEventListener("click", function () {
        cards.forEach(function (c) { c.classList.remove("selected"); });
        card.classList.add("selected");
        if (tariffInput) tariffInput.value = card.getAttribute("data-tariff");
      });
    });

    form.addEventListener("submit", function (event) {
      event.preventDefault();

      if (!form.acceptEula.checked || !form.acceptPrivacy.checked) {
        setStatus(status, "Примите условия лицензионного соглашения и политику приватности.", "err");
        return;
      }

      var payload = {
        tariffId: (tariffInput && tariffInput.value) || "personal",
        email: form.email.value.trim(),
        name: form.name.value.trim(),
        locale: "ru",
        acceptEula: !!form.acceptEula.checked,
        acceptPrivacy: !!form.acceptPrivacy.checked
      };

      setStatus(status, "Создаём заказ…", "");

      fetch(CHECKOUT_ENDPOINT, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      })
        .then(function (response) {
          if (!response.ok) {
            throw new Error("Сервер ответил кодом " + response.status);
          }
          return response.json();
        })
        .then(function (order) {
          // order: { orderId, provider, paymentUrl, amount, expiresAt } — см. docs/payments.md §4.1.
          if (order && order.paymentUrl) {
            setStatus(status, "Заказ создан (" + order.orderId + "). Переходим к оплате…", "ok");
            window.location.href = order.paymentUrl;
          } else {
            throw new Error("В ответе нет paymentUrl");
          }
        })
        .catch(function (error) {
          setStatus(
            status,
            "Онлайн-оплата пока не подключена (заготовка интеграции: " + error.message +
              "). Схема подключения — docs/payments.md.",
            "err"
          );
        });
    });
  });
})();
